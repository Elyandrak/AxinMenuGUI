// AxinMenuGUI — Features/Engine
// Archivo: MenuEngine.cs
// Responsabilidad: apertura de menús, navegación de escenas, evaluación de condiciones
//   y despacho de click events.
// NO renderiza GUI. NO carga JSON. NO manipula inventario directamente.
// La lógica de intercambio de ítems está en ExchangeEngine.cs.

using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class MenuEngine
    {
        private readonly ICoreServerAPI        _api;
        private readonly MenuRegistry          _registry;
        private readonly PlayerDataStore       _store;
        private readonly IServerNetworkChannel _channel;
        private readonly RankingService?       _ranking;
        private readonly ExchangeEngine        _exchange;
        private readonly AdminTeleportService? _adminTp;
        private readonly RandomTeleportService? _randomTp;

        private readonly Dictionary<string, (string menuId, int scene)> _activeScene = new();
        private readonly Dictionary<string, Stack<string>> _menuHistory = new();

        // Debounce: AddSkillItemGrid dispara el callback 2 veces (mousedown + mouseup)
        private readonly Dictionary<string, long> _lastClick = new();
        private const long ClickDebounceMs = 250;

        public MenuEngine(
            ICoreServerAPI api,
            MenuRegistry registry,
            PlayerDataStore store,
            IServerNetworkChannel channel,
            RankingService? ranking = null,
            AdminTeleportService? adminTp = null,
            RandomTeleportService? randomTp = null)
        {
            _api      = api;
            _registry = registry;
            _store    = store;
            _channel  = channel;
            _ranking  = ranking;
            _exchange = new ExchangeEngine(api, store);
            _adminTp  = adminTp;
            _randomTp = randomTp;
        }

        // ═══ APERTURA ════════════════════════════════════════════════

        public bool OpenMenu(IServerPlayer player, string menuId, bool addToHistory = true)
        {
            var menu = _registry.Get(menuId);
            if (menu == null)
            {
                player.SendMessage(0,
                    $"[AxinMenuGUI] Menú '{menuId}' no encontrado.", EnumChatType.Notification);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(menu.Permission)
                && !player.HasPrivilege(menu.Permission))
            {
                player.SendMessage(0,
                    "[AxinMenuGUI] No tienes permiso para abrir este menú.", EnumChatType.Notification);
                return false;
            }

            if (addToHistory)
            {
                if (!_menuHistory.ContainsKey(player.PlayerUID))
                    _menuHistory[player.PlayerUID] = new Stack<string>();
                _menuHistory[player.PlayerUID].Push(menuId);
            }

            _activeScene[player.PlayerUID] = (menuId, 0);

            // Filtrar ítems con hideOnFail antes de enviar
            var filteredMenu = FilterHiddenItems(menu, player, 0);
            var resolvedMenu  = ResolveMenuTexts(filteredMenu, player);
            _channel.SendPacket(new MenuOpenPacket { Menu = NetMenuMapper.ToNet(resolvedMenu), Scene = 0 }, player);
            return true;
        }

        // ═══ CLIC EN SLOT desde cliente ══════════════════════════════

        public void HandleSlotClick(IServerPlayer player, string menuId, int scene, int slotIndex)
        {
            string debounceKey = $"{player.PlayerUID}|{menuId}|{scene}|{slotIndex}";
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_lastClick.TryGetValue(debounceKey, out long lastMs)
                && nowMs - lastMs < ClickDebounceMs)
            {
                _api.Logger.Notification(
                    $"[AxinMenuGUI] HandleSlotClick DEBOUNCED slot={slotIndex} (gap={nowMs - lastMs}ms)");
                return;
            }
            _lastClick[debounceKey] = nowMs;

            var menu = _registry.Get(menuId);
            if (menu == null) return;
            if (!menu.Scenes.TryGetValue(scene.ToString(), out var sceneObj)) return;

            foreach (var (itemKey, item) in sceneObj.Items)
            {
                if (item.Slot != slotIndex) continue;

                if (item.Conditions.Count > 0 && !EvaluateConditions(player, item.Conditions))
                {
                    if (!string.IsNullOrWhiteSpace(item.ConditionFailMessage))
                        player.SendMessage(0, item.ConditionFailMessage, EnumChatType.Notification);
                    return;
                }

                // ── maxUses: check a nivel de ítem (una sola vez por clic) ──
                if (item.MaxUses > 0)
                {
                    var useKey = $"maxuses_{menuId}_{scene}_{itemKey}";
                    var claim  = _store.GetClaim(player.PlayerUID, useKey);
                    if (claim.Count >= item.MaxUses)
                    {
                        var limitMsg = string.IsNullOrWhiteSpace(item.MaxUsesMessage)
                            ? "[AxinMenuGUI] Has alcanzado el límite de usos de este botón."
                            : item.MaxUsesMessage;
                        player.SendMessage(0, limitMsg, EnumChatType.Notification);
                        return;
                    }
                    // Ejecutar todos los eventos y luego registrar el uso
                    ExecuteClickEvents(player, item.ClickEvents);
                    _store.IncrementClaim(player.PlayerUID, useKey);
                    return;
                }

                ExecuteClickEvents(player, item.ClickEvents);
                return;
            }
        }

        // ═══ hideOnFail — filtrado antes de enviar al cliente ════════
        // Reglas:
        //   1. Si hideOnFail=true y condiciones fallan → el ítem no se incluye.
        //   2. Slot groups: si varios ítems comparten el mismo slot, solo el primero
        //      cuyas condiciones se cumplan (o sin condiciones) se incluye;
        //      los demás del mismo slot se descartan aunque pasen sus condiciones.
        //      Si ninguno del grupo pasa, el slot queda vacío.

        private MenuDefinition FilterHiddenItems(MenuDefinition menu, IServerPlayer player, int scene)
        {
            var filtered = new MenuDefinition
            {
                Id                 = menu.Id,
                Title              = menu.Title,
                Rows               = menu.Rows,
                Theme              = menu.Theme,
                CommandAlias       = menu.CommandAlias,
                CommandAliasTarget = menu.CommandAliasTarget,
                Permission         = menu.Permission,
                OpenTriggers       = menu.OpenTriggers,
                Scenes             = new Dictionary<string, SceneDefinition>()
            };

            foreach (var (sceneKey, sceneObj) in menu.Scenes)
            {
                var filteredScene = new SceneDefinition
                {
                    DelayMs = sceneObj.DelayMs,
                    Theme   = sceneObj.Theme,
                    Items   = new Dictionary<string, ItemDefinition>()
                };

                // Slots ya ocupados en esta escena (para slot groups)
                var occupiedSlots = new System.Collections.Generic.HashSet<int>();

                // Ordenar ítems por priority (menor = primero) para slot groups deterministas.
                // Los ítems sin priority explícita tienen 0 → orden de inserción JSON como desempate.
                var orderedItems = sceneObj.Items
                    .OrderBy(kv => kv.Value.Priority)
                    .ToList();

                foreach (var (itemKey, item) in orderedItems)
                {
                    bool conditionsPass = item.Conditions.Count == 0
                        || EvaluateConditions(player, item.Conditions);

                    // hideOnFail: ocultar si condiciones fallan
                    if (item.HideOnFail && !conditionsPass)
                        continue;

                    // Slot group: si ya hay un ítem en este slot, descartar los siguientes
                    // (el primero en orden JSON que pasó sus condiciones gana)
                    if (occupiedSlots.Contains(item.Slot))
                        continue;

                    // Si las condiciones no pasan pero hideOnFail=false,
                    // se incluye el ítem (el click será bloqueado en HandleSlotClick)
                    filteredScene.Items[itemKey] = item;
                    occupiedSlots.Add(item.Slot);
                }

                filtered.Scenes[sceneKey] = filteredScene;
            }

            return filtered;
        }

        // ═══ CONDICIONES ══════════════════════════════════════════════

        public bool EvaluateConditions(
            IServerPlayer player,
            Dictionary<string, ConditionDefinition> conditions)
        {
            foreach (var (_, cond) in conditions)
            {
                bool result = EvaluateSingle(player, cond);
                if (cond.Inverted) result = !result;
                if (!result) return false;
            }
            return true;
        }

        private bool EvaluateSingle(IServerPlayer player, ConditionDefinition cond)
        {
            return cond.Type switch
            {
                "hasPrivilege"      => player.HasPrivilege(cond.Privilege),
                "hasRole"           => player.Role?.Code == cond.RoleCode,
                "hasPrivilegeLevel" => (player.Role?.PrivilegeLevel ?? -1) >= cond.MinLevel,
                "hasItem"           => _exchange.HasItem(player, cond.ItemCode, cond.Amount),
                "playerDataCompare" => PlayerDataCompare(player, cond),
                "cooldownActive"    => _store.IsCooldownActive(player.PlayerUID, cond.CooldownKey),
                _                   => LogUnknown(cond.Type)
            };
        }

        private bool PlayerDataCompare(IServerPlayer player, ConditionDefinition cond)
        {
            var raw = _store.Get(player.PlayerUID, cond.Field);
            if (double.TryParse(raw, out double dVal)
                && double.TryParse(cond.CompareValue, out double dCmp))
            {
                return cond.Operator switch
                {
                    "eq"  => dVal == dCmp,
                    "neq" => dVal != dCmp,
                    "gt"  => dVal >  dCmp,
                    "gte" => dVal >= dCmp,
                    "lt"  => dVal <  dCmp,
                    "lte" => dVal <= dCmp,
                    _     => false
                };
            }
            return cond.Operator switch
            {
                "eq"  => raw == cond.CompareValue,
                "neq" => raw != cond.CompareValue,
                _     => false
            };
        }

        private bool LogUnknown(string type)
        {
            _api.Logger.Warning($"[AxinMenuGUI] Condición desconocida: '{type}'. Evaluada como true.");
            return true;
        }

        // ═══ CLICK EVENTS ═════════════════════════════════════════════

        public void ExecuteClickEvents(
            IServerPlayer player,
            Dictionary<string, ClickEventDefinition> events,
            string inputValue = "")
        {
            foreach (var (_, ev) in events)
                ExecuteSingle(player, ev, inputValue);
        }

        private void ExecuteSingle(IServerPlayer player, ClickEventDefinition ev, string inputValue)
        {
            switch (ev.Type)
            {
                case "message":
                {
                    var msg = PlaceholderResolver.Resolve(ev.Message, player, _store, inputValue, _ranking);
                    player.SendMessage(0, msg, EnumChatType.Notification);
                    break;
                }

                case "consoleCommand":
                {
                    foreach (var cmd in ev.Commands)
                    {
                        var resolved = PlaceholderResolver.Resolve(cmd, player, _store, inputValue, _ranking);
                        var cmdText  = resolved.TrimStart('/');
                        int spaceIdx = cmdText.IndexOf(' ');
                        string cmdName = spaceIdx >= 0 ? cmdText[..spaceIdx] : cmdText;
                        string cmdArgs = spaceIdx >= 0 ? cmdText[(spaceIdx + 1)..] : "";
                        try
                        {
                            _api.ChatCommands.Execute(cmdName, new TextCommandCallingArgs
                            {
                                Caller  = new Caller
                                {
                                    Type            = EnumCallerType.Console,
                                    Player          = player,
                                    FromChatGroupId = 0
                                },
                                RawArgs = new CmdArgs(cmdArgs)
                            });
                        }
                        catch (Exception ex)
                        {
                            _api.Logger.Warning(
                                $"[AxinMenuGUI] consoleCommand '{cmdText}' error: {ex.Message}");
                        }
                    }
                    break;
                }

                case "playerCommand":
                {
                    foreach (var cmd in ev.Commands)
                    {
                        var resolved = PlaceholderResolver.Resolve(cmd, player, _store, inputValue, _ranking);
                        string cmdText = resolved.StartsWith("/") ? resolved : "/" + resolved;
                        _channel.SendPacket(new ExecuteCommandPacket { Command = cmdText }, player);
                    }
                    break;
                }

                case "closeGui":
                    _channel.SendPacket(new MenuClosePacket(), player);
                    break;

                case "openGui":
                {
                    var targetId = PlaceholderResolver.ResolveBasic(ev.GuiId, player);
                    OpenMenu(player, targetId);
                    break;
                }

                case "nextScene":
                    NavigateScene(player, +1);
                    break;

                case "previousScene":
                    NavigateScene(player, -1);
                    break;

                case "back":
                    OpenLastMenu(player);
                    break;

                case "buyItem":
                    _exchange.ExecuteExchange(player, ev, buy: true);
                    break;

                case "sellItem":
                    _exchange.ExecuteExchange(player, ev, buy: false);
                    break;

                case "giveItem":
                    _exchange.GiveItem(player, ev.ItemCode, ev.Amount);
                    break;

                case "takeItem":
                    _exchange.TakeItem(player, ev.ItemCode, ev.Amount);
                    break;

                case "setVariable":
                    _exchange.ExecuteSetVariable(player, ev, inputValue, _ranking);
                    break;

                case "teleport":
                {
                    // { "type": "teleport", "location": "x,y,z" }
                    if (!TryParseLocation(ev.Location, out double tx, out double ty, out double tz))
                    {
                        _api.Logger.Warning(
                            $"[AxinMenuGUI] teleport: formato inválido '{ev.Location}'. " +
                            "Use 'x,y,z' con números separados por coma.");
                        player.SendMessage(0,
                            "[AxinMenuGUI] Coordenadas de teletransporte inválidas.",
                            EnumChatType.Notification);
                        break;
                    }
                    AdminTeleportService.TeleportPlayerTo(player, tx, ty, tz);
                    break;
                }

                case "teleportSaved":
                {
                    // { "type": "teleportSaved", "target": "nombre_punto" }
                    if (_adminTp == null)
                    {
                        _api.Logger.Warning("[AxinMenuGUI] teleportSaved: AdminTeleportService no disponible.");
                        player.SendMessage(0,
                            "[AxinMenuGUI] Sistema de TP guardados no disponible.",
                            EnumChatType.Notification);
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(ev.Target))
                    {
                        player.SendMessage(0,
                            "[AxinMenuGUI] teleportSaved: campo 'target' vacío.",
                            EnumChatType.Notification);
                        break;
                    }
                    _adminTp.TeleportTo(player, ev.Target);
                    break;
                }

                case "randomTeleport":
                {
                    // { "type": "randomTeleport", "radiusMin": 5000, "radiusMax": 10000 }
                    // radiusMin/Max = 0 → usar defaults de config.json
                    if (_randomTp == null)
                    {
                        _api.Logger.Warning("[AxinMenuGUI] randomTeleport: RandomTeleportService no disponible.");
                        player.SendMessage(0,
                            "[AxinMenuGUI] Sistema de TP aleatorio no disponible.",
                            EnumChatType.Notification);
                        break;
                    }
                    _randomTp.TeleportRandom(player, ev.RadiusMin, ev.RadiusMax);
                    break;
                }

                default:
                    _api.Logger.Warning($"[AxinMenuGUI] Click event desconocido: '{ev.Type}'.");
                    break;
            }
        }

        // ═══ RESOLUCIÓN DE TEXTOS ANTES DE ENVIAR ════════════════════

        private MenuDefinition ResolveMenuTexts(MenuDefinition menu, IServerPlayer player)
        {
            var resolved = new MenuDefinition
            {
                Id                 = menu.Id,
                Title              = PlaceholderResolver.Resolve(menu.Title, player, _store, ranking: _ranking),
                Rows               = menu.Rows,
                Theme              = menu.Theme,
                CommandAlias       = menu.CommandAlias,
                CommandAliasTarget = menu.CommandAliasTarget,
                Permission         = menu.Permission,
                OpenTriggers       = menu.OpenTriggers,
                Scenes             = new Dictionary<string, SceneDefinition>()
            };

            foreach (var (sceneKey, sceneObj) in menu.Scenes)
            {
                var resolvedScene = new SceneDefinition
                {
                    DelayMs = sceneObj.DelayMs,
                    Theme   = sceneObj.Theme,
                    Items   = new Dictionary<string, ItemDefinition>()
                };

                foreach (var (itemKey, item) in sceneObj.Items)
                {
                    var resolvedLore = new System.Collections.Generic.List<string>();
                    foreach (var line in item.Lore)
                        resolvedLore.Add(PlaceholderResolver.Resolve(line, player, _store, ranking: _ranking));

                    var resolvedItem = new ItemDefinition
                    {
                        Slot                 = item.Slot,
                        ItemCode             = item.ItemCode,
                        Amount               = item.Amount,
                        Name                 = PlaceholderResolver.Resolve(item.Name, player, _store, ranking: _ranking),
                        Lore                 = resolvedLore,
                        HideOnFail           = item.HideOnFail,
                        ConditionFailMessage = PlaceholderResolver.Resolve(item.ConditionFailMessage, player, _store, ranking: _ranking),
                        ClickEvents          = item.ClickEvents,
                        Conditions           = item.Conditions
                    };

                    resolvedScene.Items[itemKey] = resolvedItem;
                }

                resolved.Scenes[sceneKey] = resolvedScene;
            }

            return resolved;
        }

        // ═══ NAVEGACIÓN ═══════════════════════════════════════════════

        private void NavigateScene(IServerPlayer player, int delta)
        {
            if (!_activeScene.TryGetValue(player.PlayerUID, out var current)) return;
            var menu = _registry.Get(current.menuId);
            if (menu == null) return;

            int newScene = current.scene + delta;
            if (newScene < 0 || !menu.Scenes.ContainsKey(newScene.ToString()))
            {
                player.SendMessage(0,
                    "[AxinMenuGUI] No hay más páginas en esa dirección.", EnumChatType.Notification);
                return;
            }

            _activeScene[player.PlayerUID] = (current.menuId, newScene);
            var filtered  = FilterHiddenItems(menu, player, newScene);
            var resolved  = ResolveMenuTexts(filtered, player);
            _channel.SendPacket(
                new MenuOpenPacket { Menu = NetMenuMapper.ToNet(resolved), Scene = newScene }, player);
        }

        private void OpenLastMenu(IServerPlayer player)
        {
            if (!_menuHistory.TryGetValue(player.PlayerUID, out var history) || history.Count < 2)
            {
                player.SendMessage(0, "[AxinMenuGUI] No hay menú anterior.", EnumChatType.Notification);
                return;
            }
            history.Pop();
            var prev = history.Peek();
            OpenMenu(player, prev, addToHistory: false);
        }

        // ═══ HELPERS DE TELEPORT ══════════════════════════════════════

        /// <summary>
        /// Parsea una cadena "x,y,z" en tres doubles con InvariantCulture.
        /// Acepta espacios alrededor de las comas.
        /// </summary>
        private static bool TryParseLocation(
            string location,
            out double x, out double y, out double z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(location)) return false;

            var parts = location.Split(',');
            if (parts.Length != 3) return false;

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return double.TryParse(parts[0].Trim(),
                       System.Globalization.NumberStyles.Any, ci, out x)
                && double.TryParse(parts[1].Trim(),
                       System.Globalization.NumberStyles.Any, ci, out y)
                && double.TryParse(parts[2].Trim(),
                       System.Globalization.NumberStyles.Any, ci, out z);
        }
    }
}
