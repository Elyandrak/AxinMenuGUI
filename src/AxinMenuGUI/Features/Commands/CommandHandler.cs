// AxinMenuGUI — Features/Commands
// Archivo: CommandHandler.cs
// Responsabilidad: registrar y manejar el comando /amenu y sus subcomandos.
// NO contiene lógica de menús. Delega todo en MenuEngine y MenuRegistry.

using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class CommandHandler
    {
        private readonly ICoreServerAPI _api;
        private readonly MenuRegistry   _registry;
        private readonly MenuEngine     _engine;

        // Alias ya registrados en el árbol VS — no se pueden des-registrar,
        // así que los guardamos para evitar re-registro en reload.
        private readonly System.Collections.Generic.HashSet<string> _registeredAliases = new();

        // ─────────────────────────────────────────────────────────────
        public CommandHandler(ICoreServerAPI api, MenuRegistry registry, MenuEngine engine)
        {
            _api      = api;
            _registry = registry;
            _engine   = engine;

            Register();
        }

        // ─────────────────────────────────────────────────────────────
        private void Register()
        {
            // Patrón 23 AXIN: registrar en ModsAndConfigReady para evitar
            // conflictos con el árbol de comandos de VS ya construido.
            _api.Event.ServerRunPhase(
                EnumServerRunPhase.ModsAndConfigReady,
                () => RegisterCommands());
        }

        // ─────────────────────────────────────────────────────────────
        private void RegisterCommands()
        {
            _api.ChatCommands
                .Create("amenu")
                .WithDescription("AxinMenuGUI — gestión de menús")
                .RequiresPrivilege(Privilege.chat)

                // /amenu open <id> [jugador]
                .BeginSubCommand("open")
                    .WithDescription("Abre un menú para ti o para otro jugador")
                    .WithArgs(
                        _api.ChatCommands.Parsers.Word("menuId"),
                        _api.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(OnOpen)
                .EndSubCommand()

                // /amenu reload
                .BeginSubCommand("reload")
                    .WithDescription("Recarga todos los menús desde disco (sin reiniciar)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnReload)
                .EndSubCommand()

                // /amenu list
                .BeginSubCommand("list")
                    .WithDescription("Lista los menús cargados")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnList)
                .EndSubCommand()

                // /amenu info <id>
                .BeginSubCommand("info")
                    .WithDescription("Muestra detalles de un menú")
                    .WithArgs(_api.ChatCommands.Parsers.Word("menuId"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnInfo)
                .EndSubCommand();

            _api.Logger.Notification("[AxinMenuGUI] Comandos registrados: /amenu");

            // Registrar alias de menús ya cargados
            RegisterAliases();
        }

        // ─────────────────────────────────────────────────────────────
        // Registra /alias para cada menú con commandAlias definido.
        // Los alias ya registrados se saltan (VS no permite des-registrar).
        // Se llama en startup y en cada reload.
        public void RegisterAliases()
        {
            foreach (var menu in _registry.GetAll())
            {
                var alias = menu.CommandAlias?.Trim();
                if (string.IsNullOrWhiteSpace(alias)) continue;
                if (_registeredAliases.Contains(alias))
                {
                    _api.Logger.Notification($"[AxinMenuGUI] Alias '/{alias}' ya registrado — saltando.");
                    continue;
                }

                var capturedMenu  = menu;
                var capturedAlias = alias;
                var targetMode    = (menu.CommandAliasTarget ?? "DISABLED").ToUpperInvariant();

                try
                {
                    var cmd = _api.ChatCommands
                        .Create(capturedAlias)
                        .WithDescription($"Abre el menú '{capturedMenu.Id}'")
                        .RequiresPrivilege(Privilege.chat);

                    if (targetMode == "DISABLED")
                    {
                        // Sin argumento — siempre abre para quien ejecuta
                        cmd.HandleWith(a =>
                        {
                            var p = a.Caller.Player as IServerPlayer;
                            if (p == null) return TextCommandResult.Error("Solo jugadores.");
                            _engine.OpenMenu(p, capturedMenu.Id);
                            return TextCommandResult.Success();
                        });
                    }
                    else
                    {
                        // OPTIONAL o REQUIRED — argumento jugador target
                        bool required = targetMode == "REQUIRED";
                        cmd.WithArgs(required
                                ? _api.ChatCommands.Parsers.Word("playerName")
                                : _api.ChatCommands.Parsers.OptionalWord("playerName"))
                           .HandleWith(a =>
                           {
                               var p = a.Caller.Player as IServerPlayer;
                               if (p == null) return TextCommandResult.Error("Solo jugadores.");

                               var targetName = a.ArgCount > 0 ? (string)a[0] : null;

                               if (!string.IsNullOrWhiteSpace(targetName))
                               {
                                   if (!p.HasPrivilege(Privilege.controlserver))
                                       return TextCommandResult.Error("No tienes permiso para abrir menús a otros jugadores.");

                                   var target = _api.Server.Players
                                       .FirstOrDefault(pl => pl.PlayerName
                                           .Equals(targetName, StringComparison.OrdinalIgnoreCase));

                                   if (target == null)
                                       return TextCommandResult.Error($"Jugador '{targetName}' no encontrado.");

                                   _engine.OpenMenu(target, capturedMenu.Id);
                                   return TextCommandResult.Success($"Menú '{capturedMenu.Id}' abierto para {target.PlayerName}.");
                               }

                               if (required)
                                   return TextCommandResult.Error($"Uso: /{capturedAlias} <jugador>");

                               _engine.OpenMenu(p, capturedMenu.Id);
                               return TextCommandResult.Success();
                           });
                    }

                    _registeredAliases.Add(capturedAlias);
                    _api.Logger.Notification($"[AxinMenuGUI] Alias registrado: /{capturedAlias} → '{capturedMenu.Id}' (target={targetMode})");
                }
                catch (Exception ex)
                {
                    _api.Logger.Warning($"[AxinMenuGUI] Error al registrar alias '/{alias}': {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // HANDLERS
        // ═══════════════════════════════════════════════════════════

        private TextCommandResult OnOpen(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null)
                return TextCommandResult.Error("Solo jugadores pueden usar este comando.");

            var menuId     = (string)args[0];
            var targetName = args.ArgCount > 1 ? (string)args[1] : null;

            // Abrir para otro jugador
            if (!string.IsNullOrWhiteSpace(targetName))
            {
                if (!player.HasPrivilege(Privilege.controlserver))
                    return TextCommandResult.Error("No tienes permiso para abrir menús a otros jugadores.");

                var target = _api.Server.Players
                    .FirstOrDefault(p => p.PlayerName
                        .Equals(targetName, StringComparison.OrdinalIgnoreCase));

                if (target == null)
                    return TextCommandResult.Error($"Jugador '{targetName}' no encontrado.");

                _engine.OpenMenu(target, menuId);
                return TextCommandResult.Success($"Menú '{menuId}' abierto para {target.PlayerName}.");
            }

            // Abrir para uno mismo
            _engine.OpenMenu(player, menuId);
            return TextCommandResult.Success();
        }

        // ─────────────────────────────────────────────────────────────
        private TextCommandResult OnReload(TextCommandCallingArgs args)
        {
            _registry.LoadAll();
            RegisterAliases(); // registra alias de menús nuevos (los ya registrados se saltan)
            return TextCommandResult.Success(
                $"[AxinMenuGUI] Recarga completada. {_registry.Count} menú(s) cargado(s).");

        }

        // ─────────────────────────────────────────────────────────────
        private TextCommandResult OnList(TextCommandCallingArgs args)
        {
            var ids = _registry.GetAllIds().ToList();

            if (ids.Count == 0)
                return TextCommandResult.Success("[AxinMenuGUI] No hay menús cargados.");

            var sb = new StringBuilder();
            sb.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> Menús cargados ({ids.Count}):");
            foreach (var id in ids)
                sb.AppendLine($"  • <font color='#e9ddce'>{id}</font>");

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        // ─────────────────────────────────────────────────────────────
        private TextCommandResult OnInfo(TextCommandCallingArgs args)
        {
            var menuId = (string)args[0];
            var menu   = _registry.Get(menuId);

            if (menu == null)
                return TextCommandResult.Error($"Menú '{menuId}' no encontrado.");

            var sb = new StringBuilder();
            sb.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> Info: {menu.Id}");
            sb.AppendLine($"  Título:   {menu.Title}");
            sb.AppendLine($"  Filas:    {menu.Rows}");
            sb.AppendLine($"  Alias:    {(string.IsNullOrWhiteSpace(menu.CommandAlias) ? "—" : "/" + menu.CommandAlias)}");
            sb.AppendLine($"  Permiso:  {(string.IsNullOrWhiteSpace(menu.Permission)   ? "ninguno" : menu.Permission)}");
            sb.AppendLine($"  Escenas:  {menu.Scenes.Count}");

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }
    }
}
