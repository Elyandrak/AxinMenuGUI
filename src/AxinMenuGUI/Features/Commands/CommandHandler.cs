// AxinMenuGUI — Features/Commands
// Archivo: CommandHandler.cs
// Responsabilidad: registrar y manejar /amenu y sus subcomandos.
//
// CAMBIO v0.6.4: añadido subcomando "click" con sub-subcomandos "open" y "delete".
// Solo modificación: campo _blockClick + método OnClickOpen + OnClickDelete
//                    + registro del subcomando en RegisterCommands().
// El resto del archivo es idéntico a v0.6.3.

using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class CommandHandler
    {
        private readonly ICoreServerAPI   _api;
        private readonly MenuRegistry     _registry;
        private readonly MenuEngine       _engine;
        private readonly PlayerDataStore  _store;
        private readonly RankingService   _ranking;
        private BlockClickHandlerServer?  _blockClick; // inyectado después de construcción

        private readonly System.Collections.Generic.HashSet<string> _registeredAliases = new();

        public CommandHandler(
            ICoreServerAPI api,
            MenuRegistry registry,
            MenuEngine engine,
            PlayerDataStore store,
            RankingService ranking)
        {
            _api      = api;
            _registry = registry;
            _engine   = engine;
            _store    = store;
            _ranking  = ranking;

            Register();
        }

        /// <summary>
        /// Inyecta la referencia al BlockClickHandler.
        /// Llamado desde AxinMenuGuiMod.StartServerSide después de construir ambos.
        /// </summary>
        public void SetBlockClickHandler(BlockClickHandlerServer handler)
            => _blockClick = handler;

        private void Register()
        {
            _api.Event.ServerRunPhase(
                EnumServerRunPhase.ModsAndConfigReady,
                () => RegisterCommands());
        }

        private void RegisterCommands()
        {
            _api.ChatCommands
                .Create("amenu")
                .WithDescription("AxinMenuGUI — gestión de menús")
                .RequiresPrivilege(Privilege.chat)

                .BeginSubCommand("open")
                    .WithDescription("Abre un menú para ti o para otro jugador")
                    .WithArgs(
                        _api.ChatCommands.Parsers.Word("menuId"),
                        _api.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(OnOpen)
                .EndSubCommand()

                .BeginSubCommand("reload")
                    .WithDescription("Recarga todos los menús desde disco")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnReload)
                .EndSubCommand()

                .BeginSubCommand("list")
                    .WithDescription("Lista los menús cargados")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnList)
                .EndSubCommand()

                .BeginSubCommand("info")
                    .WithDescription("Muestra detalles de un menú")
                    .WithArgs(_api.ChatCommands.Parsers.Word("menuId"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnInfo)
                .EndSubCommand()

                .BeginSubCommand("player")
                    .WithDescription("Muestra o gestiona estadísticas de un jugador")
                    .WithArgs(
                        _api.ChatCommands.Parsers.Word("playerName"),
                        _api.ChatCommands.Parsers.OptionalWord("action"),
                        _api.ChatCommands.Parsers.OptionalWord("param1"),
                        _api.ChatCommands.Parsers.OptionalWord("param2"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnPlayer)
                .EndSubCommand()

                .BeginSubCommand("ranking")
                    .WithDescription("Muestra el ranking de jugadores")
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(OnRanking)
                .EndSubCommand()

                // ── NUEVO: /amenu click ───────────────────────────────
                .BeginSubCommand("click")
                    .WithDescription("Vincula o desvincula un menú a un bloque")
                    .RequiresPrivilege(Privilege.controlserver)

                    .BeginSubCommand("open")
                        .WithDescription("Vincula un bloque a un menú (clic derecho para confirmar)")
                        .WithArgs(_api.ChatCommands.Parsers.Word("menuId"))
                        .HandleWith(OnClickOpen)
                    .EndSubCommand()

                    .BeginSubCommand("delete")
                        .WithDescription("Desvincula un bloque de su menú (clic derecho para confirmar)")
                        .HandleWith(OnClickDelete)
                    .EndSubCommand()

                .EndSubCommand();
                // ─────────────────────────────────────────────────────

            _api.Logger.Notification("[AxinMenuGUI] Comandos registrados: /amenu");
            RegisterAliases();
        }

        public void RegisterAliases()
        {
            foreach (var menu in _registry.GetAll())
            {
                var alias = menu.CommandAlias?.Trim();
                if (string.IsNullOrWhiteSpace(alias)) continue;
                if (_registeredAliases.Contains(alias)) continue;

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
                        bool required = targetMode == "REQUIRED";
                        cmd.WithArgs(required
                                ? _api.ChatCommands.Parsers.Word("playerName")
                                : _api.ChatCommands.Parsers.OptionalWord("playerName"))
                           .HandleWith(a =>
                           {
                               var p = a.Caller.Player as IServerPlayer;
                               if (p == null) return TextCommandResult.Error("Solo jugadores.");
                               var targetName = a[0] as string;
                               if (!string.IsNullOrWhiteSpace(targetName))
                               {
                                   if (!p.HasPrivilege(Privilege.controlserver))
                                       return TextCommandResult.Error("Sin permiso.");
                                   var tgt = _api.Server.Players
                                       .FirstOrDefault(pl => pl.PlayerName
                                           .Equals(targetName, StringComparison.OrdinalIgnoreCase));
                                   if (tgt == null)
                                       return TextCommandResult.Error($"Jugador '{targetName}' no encontrado.");
                                   _engine.OpenMenu(tgt, capturedMenu.Id);
                                   return TextCommandResult.Success($"Menú abierto para {tgt.PlayerName}.");
                               }
                               if (required) return TextCommandResult.Error($"Uso: /{capturedAlias} <jugador>");
                               _engine.OpenMenu(p, capturedMenu.Id);
                               return TextCommandResult.Success();
                           });
                    }

                    _registeredAliases.Add(capturedAlias);
                    _api.Logger.Notification(
                        $"[AxinMenuGUI] Alias registrado: /{capturedAlias} → '{capturedMenu.Id}'");
                }
                catch (Exception ex)
                {
                    _api.Logger.Warning(
                        $"[AxinMenuGUI] Error al registrar alias '/{alias}': {ex.Message}");
                }
            }
        }

        // ═══ HANDLERS ════════════════════════════════════════════════

        private TextCommandResult OnOpen(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Solo jugadores.");

            var menuId     = (string)args[0];
            var targetName = args[1] as string;

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                if (!player.HasPrivilege(Privilege.controlserver))
                    return TextCommandResult.Error("Sin permiso para abrir menús a otros.");
                var target = _api.Server.Players
                    .FirstOrDefault(p => p.PlayerName
                        .Equals(targetName, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                    return TextCommandResult.Error($"Jugador '{targetName}' no encontrado.");
                _engine.OpenMenu(target, menuId);
                return TextCommandResult.Success($"Menú '{menuId}' abierto para {target.PlayerName}.");
            }

            _engine.OpenMenu(player, menuId);
            return TextCommandResult.Success();
        }

        private TextCommandResult OnReload(TextCommandCallingArgs args)
        {
            _registry.LoadAll();
            RegisterAliases();
            _ranking.InvalidateCache();
            return TextCommandResult.Success(
                $"[AxinMenuGUI] Recarga completada. {_registry.Count} menú(s) cargado(s).");
        }

        private TextCommandResult OnList(TextCommandCallingArgs args)
        {
            var ids = _registry.GetAllIds().ToList();
            if (ids.Count == 0)
                return TextCommandResult.Success("[AxinMenuGUI] No hay menús cargados.");
            var sb = new StringBuilder();
            sb.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> Menús ({ids.Count}):");
            foreach (var id in ids)
                sb.AppendLine($"  • <font color='#e9ddce'>{id}</font>");
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        private TextCommandResult OnInfo(TextCommandCallingArgs args)
        {
            var menuId = (string)args[0];
            var menu   = _registry.Get(menuId);
            if (menu == null) return TextCommandResult.Error($"Menú '{menuId}' no encontrado.");
            var sb = new StringBuilder();
            sb.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> Info: {menu.Id}");
            sb.AppendLine($"  Título:  {menu.Title}");
            sb.AppendLine($"  Filas:   {menu.Rows}");
            sb.AppendLine($"  Alias:   {(string.IsNullOrWhiteSpace(menu.CommandAlias) ? "—" : "/" + menu.CommandAlias)}");
            sb.AppendLine($"  Permiso: {(string.IsNullOrWhiteSpace(menu.Permission) ? "ninguno" : menu.Permission)}");
            sb.AppendLine($"  Escenas: {menu.Scenes.Count}");
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        private TextCommandResult OnPlayer(TextCommandCallingArgs args)
        {
            var playerName = (string)args[0];
            var action     = (args[1] as string)?.ToLowerInvariant() ?? "info";
            var param1     = (args[2] as string) ?? "";
            var param2     = (args[3] as string) ?? "";

            var target = _api.Server.Players
                .FirstOrDefault(p => p.PlayerName
                    .Equals(playerName, StringComparison.OrdinalIgnoreCase));

            string uid = target?.PlayerUID ?? "";

            if (string.IsNullOrEmpty(uid))
            {
                var dataFolder = System.IO.Path.Combine(
                    _api.DataBasePath, "ModConfig", "AxinMenuGUI", "playerdata");
                if (System.IO.Directory.Exists(dataFolder))
                {
                    foreach (var file in System.IO.Directory.GetFiles(dataFolder, "*.json"))
                    {
                        try
                        {
                            var data = JsonConvert.DeserializeObject<PlayerData>(
                                System.IO.File.ReadAllText(file));
                            if (data?.PlayerName?.Equals(playerName,
                                    StringComparison.OrdinalIgnoreCase) == true)
                            { uid = data.Uid; break; }
                        }
                        catch { }
                    }
                }
            }

            if (string.IsNullOrEmpty(uid))
                return TextCommandResult.Error($"Jugador '{playerName}' no encontrado.");

            var pdata = _store.GetOrCreate(uid, playerName);

            switch (action)
            {
                case "info":
                case "stats":
                {
                    var s   = pdata.Stats;
                    var pos = _ranking.GetPlayerPosition(pdata.PlayerName);
                    var sb  = new StringBuilder();
                    sb.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> Stats: <font color='cyan'>{pdata.PlayerName}</font>");
                    sb.AppendLine($"  timeRealSeconds:       {s.TimeRealSeconds:F0}  ({FormatTime(s.TimeRealSeconds)})");
                    sb.AppendLine($"  timeGameDays:          {s.TimeGameDays:F2}");
                    sb.AppendLine($"  deaths:                {s.Deaths}");
                    sb.AppendLine($"  mobKillsAll:           {s.MobKillsAll}");
                    sb.AppendLine($"  mobKillsHostile:       {s.MobKillsHostile}");
                    sb.AppendLine($"  mobKillsHostilePoints: {s.MobKillsHostilePoints:F1}");
                    sb.AppendLine($"  playerKills:           {s.PlayerKills}");
                    sb.AppendLine($"  ranking.position:      {(pos > 0 ? $"#{pos}" : "—")}");
                    return TextCommandResult.Success(sb.ToString().TrimEnd());
                }

                case "reset":
                    switch (param1.ToLowerInvariant())
                    {
                        case "stats":
                            _store.ResetStats(uid);
                            _ranking.InvalidateCache();
                            return TextCommandResult.Success($"[AxinMenuGUI] Stats de {playerName} reseteadas.");
                        case "cooldowns":
                            _store.ResetCooldowns(uid);
                            return TextCommandResult.Success($"[AxinMenuGUI] Cooldowns de {playerName} eliminados.");
                        case "claims":
                            _store.ResetClaims(uid);
                            return TextCommandResult.Success($"[AxinMenuGUI] Claims de {playerName} eliminados.");
                        default:
                            return TextCommandResult.Error("Uso: /amenu player <nombre> reset [stats|cooldowns|claims]");
                    }

                case "set":
                    if (string.IsNullOrWhiteSpace(param1) || string.IsNullOrWhiteSpace(param2))
                        return TextCommandResult.Error("Uso: /amenu player <nombre> set <campo> <valor>");
                    _store.Set(uid, playerName, param1, param2);
                    return TextCommandResult.Success($"[AxinMenuGUI] {playerName}.{param1} = {param2}");

                case "fields":
                    if (pdata.Fields.Count == 0)
                        return TextCommandResult.Success($"[AxinMenuGUI] {playerName} no tiene fields.");
                    var sbf = new StringBuilder();
                    sbf.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> Fields de {playerName}:");
                    foreach (var (k, v) in pdata.Fields)
                        sbf.AppendLine($"  {k} = {v}");
                    return TextCommandResult.Success(sbf.ToString().TrimEnd());

                default:
                    return TextCommandResult.Error(
                        "Acciones: info | reset [stats|cooldowns|claims] | set <campo> <valor> | fields");
            }
        }

        private TextCommandResult OnRanking(TextCommandCallingArgs args)
        {
            var text = _ranking.FormatRanking();
            return TextCommandResult.Success(text);
        }

        // ─── NUEVO: handlers de /amenu click ─────────────────────────

        private TextCommandResult OnClickOpen(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Solo jugadores.");
            if (_blockClick == null)
                return TextCommandResult.Error("[AxinMenuGUI] BlockClickHandler no inicializado.");

            var menuId = (string)args[0];

            if (!_blockClick.BeginPendingLink(player, menuId))
                return TextCommandResult.Error(
                    Lang(player, "amenu.click.menu-not-exist", menuId));

            return TextCommandResult.Success(
                Lang(player, "amenu.click.await-link", menuId));
        }

        private TextCommandResult OnClickDelete(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Solo jugadores.");
            if (_blockClick == null)
                return TextCommandResult.Error("[AxinMenuGUI] BlockClickHandler no inicializado.");

            _blockClick.BeginPendingDelete(player);
            return TextCommandResult.Success(
                Lang(player, "amenu.click.await-delete"));
        }

        // ─── Localización interna ─────────────────────────────────────

        private string Lang(IServerPlayer player, string key, params object[] args)
        {
            // VS resuelve lang keys del mod automáticamente con el idioma del servidor.
            var raw = _api.World.Config.GetString(key, key);
            return args.Length > 0 ? string.Format(raw, args) : raw;
        }

        // ─────────────────────────────────────────────────────────────

        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}
