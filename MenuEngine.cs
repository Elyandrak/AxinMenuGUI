// AxinMenuGUI — Features/Engine
// Archivo: MenuEngine.cs
// Responsabilidad: evaluar condiciones y ejecutar click events.
// NO renderiza GUI. NO carga JSON. Solo lógica de ejecución.

using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class MenuEngine
    {
        private readonly ICoreServerAPI        _api;
        private readonly MenuRegistry          _registry;
        private readonly PlayerDataStore       _store;
        private readonly IServerNetworkChannel _channel;

        private readonly Dictionary<string, (string menuId, int scene)> _activeScene = new();
        private readonly Dictionary<string, Stack<string>> _menuHistory = new();

        // Debounce: AddSkillItemGrid dispara el callback 2 veces (mousedown + mouseup)
        private readonly Dictionary<string, long> _lastClick = new();
        private const long ClickDebounceMs = 250;

        public MenuEngine(
            ICoreServerAPI api,
            MenuRegistry registry,
            PlayerDataStore store,
            IServerNetworkChannel channel)
        {
            _api      = api;
            _registry = registry;
            _store    = store;
            _channel  = channel;
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
            _channel.SendPacket(new MenuOpenPacket { Menu = NetMenuMapper.ToNet(filteredMenu), Scene = 0 }, player);
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

            foreach (var (_, item) in sceneObj.Items)
            {
                if (item.Slot != slotIndex) continue;

                if (item.Conditions.Count > 0 && !EvaluateConditions(player, item.Conditions))
                {
                    if (!string.IsNullOrWhiteSpace(item.ConditionFailMessage))
                        player.SendMessage(0, item.ConditionFailMessage, EnumChatType.Notification);
                    return;
                }

                ExecuteClickEvents(player, item.ClickEvents);
                return;
            }
        }

        // ═══ hideOnFail — filtrado antes de enviar al cliente ════════

        private MenuDefinition FilterHiddenItems(MenuDefinition menu, IServerPlayer player, int scene)
        {
            // Clonar superficialmente para no mutar el original
            var filtered = new MenuDefinition
            {
                Id                 = menu.Id,
                Title              = menu.Title,
                Rows               = menu.Rows,
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
                    Items   = new Dictionary<string, ItemDefinition>()
                };

                foreach (var (itemKey, item) in sceneObj.Items)
                {
                    if (item.HideOnFail
                        && item.Conditions.Count > 0
                        && !EvaluateConditions(player, item.Conditions))
                        continue; // ocultar este ítem

                    filteredScene.Items[itemKey] = item;
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
                "hasItem"           => HasItem(player, cond.ItemCode, cond.Amount),
                "playerDataCompare" => PlayerDataCompare(player, cond),
                "cooldownActive"    => _store.IsCooldownActive(player.PlayerUID, cond.CooldownKey),
                _                   => LogUnknown(cond.Type)
            };
        }

        private bool HasItem(IServerPlayer player, string itemCode, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return false;
            int total = 0;
            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                foreach (var slot in inv)
                {
                    if (slot?.Itemstack?.Collectible?.Code?.ToString() == itemCode)
                        total += slot.Itemstack.StackSize;
                    if (total >= amount) return true;
                }
            }
            return total >= amount;
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
            // Comparación de strings
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
                    var msg = PlaceholderResolver.Resolve(ev.Message, player, _store, inputValue);
                    player.SendMessage(0, msg, EnumChatType.Notification);
                    break;
                }

                case "consoleCommand":
                {
                    foreach (var cmd in ev.Commands)
                    {
                        var resolved = PlaceholderResolver.Resolve(cmd, player, _store, inputValue);
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
                        var resolved = PlaceholderResolver.Resolve(cmd, player, _store, inputValue);
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
                    ExecuteExchange(player, ev, buy: true);
                    break;

                case "sellItem":
                    ExecuteExchange(player, ev, buy: false);
                    break;

                case "giveItem":
                    GiveItem(player, ev.ItemCode, ev.Amount);
                    break;

                case "takeItem":
                    TakeItem(player, ev.ItemCode, ev.Amount);
                    break;

                case "setVariable":
                    ExecuteSetVariable(player, ev, inputValue);
                    break;

                case "teleport":
                    player.SendMessage(0,
                        "[AxinMenuGUI] teleport: pendiente (Bloque 2.2)", EnumChatType.Notification);
                    break;

                default:
                    _api.Logger.Warning($"[AxinMenuGUI] Click event desconocido: '{ev.Type}'.");
                    break;
            }
        }

        // ═══ EXCHANGE (buyItem / sellItem) ════════════════════════════

        private void ExecuteExchange(IServerPlayer player, ClickEventDefinition ev, bool buy)
        {
            var costList = buy ? ev.Cost : ev.Give;
            var giveList = buy ? ev.Give : ev.Cost;
            var uid      = player.PlayerUID;

            // ── Verificar límites por jugador ──
            if (ev.Limits?.PerPlayer != null)
            {
                var limits = ev.Limits.PerPlayer;
                var claimKey = $"exchange_{ev.Type}_{string.Join("_", giveList.Select(g => g.Item))}";

                if (limits.MaxTotal > 0)
                {
                    var claim = _store.GetClaim(uid, claimKey);
                    if (claim.Count >= limits.MaxTotal)
                    {
                        var msg = string.IsNullOrWhiteSpace(ev.ConditionFailMessage)
                            ? "[AxinMenuGUI] Has alcanzado el límite máximo de usos."
                            : ev.ConditionFailMessage;
                        player.SendMessage(0, msg, EnumChatType.Notification);
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(limits.Cooldown))
                {
                    var cooldownKey = $"cd_{claimKey}";
                    if (_store.IsCooldownActive(uid, cooldownKey))
                    {
                        var msg = string.IsNullOrWhiteSpace(ev.ConditionFailMessage)
                            ? "[AxinMenuGUI] Este artículo está en cooldown."
                            : ev.ConditionFailMessage;
                        player.SendMessage(0, msg, EnumChatType.Notification);
                        return;
                    }
                }
            }

            // ── Verificar que el jugador tiene el coste ──
            foreach (var cost in costList)
            {
                if (!HasItem(player, cost.Item, cost.Amount))
                {
                    var msg = string.IsNullOrWhiteSpace(ev.ConditionFailMessage)
                        ? $"[AxinMenuGUI] No tienes suficiente {cost.Item} (x{cost.Amount})."
                        : ev.ConditionFailMessage;
                    player.SendMessage(0, msg, EnumChatType.Notification);
                    return;
                }
            }

            // ── Consumir coste ──
            if (ev.Consume)
            {
                foreach (var cost in costList)
                    TakeItem(player, cost.Item, cost.Amount);
            }

            // ── Entregar ítems ──
            foreach (var give in giveList)
                GiveItem(player, give.Item, give.Amount);

            // ── Registrar claim y cooldown ──
            if (ev.Limits?.PerPlayer != null)
            {
                var claimKey = $"exchange_{ev.Type}_{string.Join("_", giveList.Select(g => g.Item))}";

                if (ev.Limits.PerPlayer.MaxTotal > 0)
                    _store.IncrementClaim(uid, claimKey);

                if (!string.IsNullOrWhiteSpace(ev.Limits.PerPlayer.Cooldown))
                    _store.SetCooldown(uid, $"cd_{claimKey}", ev.Limits.PerPlayer.Cooldown);
            }
        }

        // ═══ GIVE / TAKE ITEMS ════════════════════════════════════════

        private void GiveItem(IServerPlayer player, string itemCode, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            var loc   = new AssetLocation(itemCode);
            var item  = _api.World.GetItem(loc);
            ItemStack? stack = null;

            if (item != null)
                stack = new ItemStack(item, amount);
            else
            {
                var block = _api.World.GetBlock(loc);
                if (block != null) stack = new ItemStack(block, amount);
            }

            if (stack == null)
            {
                _api.Logger.Warning($"[AxinMenuGUI] giveItem: ítem '{itemCode}' no encontrado.");
                return;
            }

            if (!player.InventoryManager.TryGiveItemstack(stack, true))
            {
                // Inventario lleno — soltar al suelo
                _api.World.SpawnItemEntity(stack, player.Entity.ServerPos.XYZ);
            }
        }

        private void TakeItem(IServerPlayer player, string itemCode, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            int remaining = amount;
            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                foreach (var slot in inv)
                {
                    if (slot?.Itemstack?.Collectible?.Code?.ToString() != itemCode) continue;
                    int take = Math.Min(remaining, slot.Itemstack.StackSize);
                    slot.Itemstack.StackSize -= take;
                    if (slot.Itemstack.StackSize <= 0) slot.Itemstack = null;
                    slot.MarkDirty();
                    remaining -= take;
                    if (remaining <= 0) return;
                }
            }
        }

        // ═══ SET VARIABLE ═════════════════════════════════════════════

        private void ExecuteSetVariable(IServerPlayer player, ClickEventDefinition ev, string inputValue)
        {
            if (string.IsNullOrWhiteSpace(ev.Variable)) return;
            var uid = player.PlayerUID;

            var resolvedValue = PlaceholderResolver.Resolve(ev.Value, player, _store, inputValue);

            if (ev.Operation == "set")
            {
                _store.Set(uid, player.PlayerName, ev.Variable, resolvedValue);
                return;
            }

            // Operaciones numéricas
            double current = double.TryParse(_store.Get(uid, ev.Variable), out double c) ? c : 0;
            double operand = double.TryParse(resolvedValue, out double o) ? o : 0;

            double result = ev.Operation switch
            {
                "add"      => current + operand,
                "subtract" => current - operand,
                "multiply" => current * operand,
                "divide"   => operand != 0 ? current / operand : current,
                _          => current
            };

            _store.Set(uid, player.PlayerName, ev.Variable, result.ToString());
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
            var filtered = FilterHiddenItems(menu, player, newScene);
            _channel.SendPacket(
                new MenuOpenPacket { Menu = NetMenuMapper.ToNet(filtered), Scene = newScene }, player);
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
    }
}
