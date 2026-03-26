// AxinMenuGUI — Features/Engine
// Archivo: MenuEngine.cs
// Responsabilidad: evaluar condiciones y ejecutar click events.
// NO renderiza GUI. NO carga JSON. Solo lógica de ejecución.

using System;
using System.Collections.Generic;
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

        private readonly Dictionary<string, (string menuId, int scene)> _activeScene = new();
        private readonly Dictionary<string, Stack<string>> _menuHistory = new();

        // Debounce: evita doble disparo de AddSkillItemGrid (mousedown + mouseup)
        // Clave: "{uid}|{menuId}|{scene}|{slot}" → timestamp último disparo
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

            // Enviar paquete al cliente para abrir el GUI
            _channel.SendPacket(new MenuOpenPacket { Menu = NetMenuMapper.ToNet(menu), Scene = 0 }, player);
            return true;
        }

        // ═══ CLIC EN SLOT desde cliente ══════════════════════════════

        public void HandleSlotClick(IServerPlayer player, string menuId, int scene, int slotIndex)
        {
            // Debounce: AddSkillItemGrid dispara el callback 2 veces (mousedown + mouseup).
            // Descartamos el segundo disparo si llega dentro de ClickDebounceMs ms.
            string debounceKey = $"{player.PlayerUID}|{menuId}|{scene}|{slotIndex}";
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_lastClick.TryGetValue(debounceKey, out long lastMs) && nowMs - lastMs < ClickDebounceMs)
            {
                _api.Logger.Notification($"[AxinMenuGUI] HandleSlotClick DEBOUNCED slot={slotIndex} (gap={nowMs - lastMs}ms)");
                return;
            }
            _lastClick[debounceKey] = nowMs;

            _api.Logger.Notification($"[AxinMenuGUI-DIAG] HandleSlotClick menuId={menuId} scene={scene} slot={slotIndex}");

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
                "hasPrivilege" => player.HasPrivilege(cond.Privilege),
                "hasItem"      => HasItemStub(player, cond.ItemCode, cond.Amount),
                _              => LogUnknownCondition(cond.Type)
            };
        }

        private bool HasItemStub(IServerPlayer player, string itemCode, int amount)
        {
            _api.Logger.Warning(
                $"[AxinMenuGUI] hasItem '{itemCode}' x{amount} — stub, siempre true. Implementar en Bloque 2.1.");
            return true;
        }

        private bool LogUnknownCondition(string type)
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
                        string cmdName = spaceIdx >= 0 ? cmdText.Substring(0, spaceIdx) : cmdText;
                        string cmdArgs = spaceIdx >= 0 ? cmdText.Substring(spaceIdx + 1) : "";

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
                            _api.Logger.Warning($"[AxinMenuGUI] consoleCommand '{cmdText}' error: {ex.Message}");
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

                        _api.Logger.Notification($"[AxinMenuGUI-DIAG] playerCommand enviando al cliente: '{cmdText}' para {player.PlayerName}");

                        // El cliente ejecuta el comando como si el jugador lo escribiera en el chat
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

                case "giveItem":
                    player.SendMessage(0, "[AxinMenuGUI] giveItem: pendiente (Bloque 2.1)", EnumChatType.Notification);
                    break;

                case "takeItem":
                    player.SendMessage(0, "[AxinMenuGUI] takeItem: pendiente (Bloque 2.1)", EnumChatType.Notification);
                    break;

                case "teleport":
                    player.SendMessage(0, "[AxinMenuGUI] teleport: pendiente (Bloque 2.2)", EnumChatType.Notification);
                    break;

                default:
                    _api.Logger.Warning($"[AxinMenuGUI] Click event desconocido: '{ev.Type}'.");
                    break;
            }
        }

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
            _channel.SendPacket(new MenuOpenPacket { Menu = NetMenuMapper.ToNet(menu), Scene = newScene }, player);
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
