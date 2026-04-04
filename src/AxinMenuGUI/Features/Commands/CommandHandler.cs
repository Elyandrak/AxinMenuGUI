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
        private readonly ICoreServerAPI        _api;
        private readonly MenuRegistry          _registry;
        private readonly MenuEngine            _engine;
        private readonly PlayerDataStore       _store;
        private readonly RankingService        _ranking;
        private readonly AdminTeleportService? _adminTp;
        private readonly RandomTeleportService? _randomTp;
        private BlockClickHandlerServer?       _blockClick; // inyectado después de construcción

        private readonly System.Collections.Generic.HashSet<string> _registeredAliases = new();

        public CommandHandler(
            ICoreServerAPI api,
            MenuRegistry registry,
            MenuEngine engine,
            PlayerDataStore store,
            RankingService ranking,
            AdminTeleportService? adminTp = null,
            RandomTeleportService? randomTp = null)
        {
            _api      = api;
            _registry = registry;
            _engine   = engine;
            _store    = store;
            _ranking  = ranking;
            _adminTp  = adminTp;
            _randomTp = randomTp;

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

            // ── /atp — puntos de TP admin ─────────────────────────────
            _api.ChatCommands
                .Create("atp")
                .WithDescription("AxinMenuGUI — puntos de teletransporte admin")
                .RequiresPrivilege(Privilege.controlserver)

                .BeginSubCommand("set")
                    .WithDescription("Guarda tu posición actual como punto TP")
                    .WithArgs(_api.ChatCommands.Parsers.Word("name"))
                    .HandleWith(OnAtpSet)
                .EndSubCommand()

                .BeginSubCommand("setat")
                    .WithDescription("Guarda un punto TP en coordenadas específicas")
                    .WithArgs(
                        _api.ChatCommands.Parsers.Word("name"),
                        _api.ChatCommands.Parsers.Word("x"),
                        _api.ChatCommands.Parsers.Word("y"),
                        _api.ChatCommands.Parsers.Word("z"))
                    .HandleWith(OnAtpSetAt)
                .EndSubCommand()

                .BeginSubCommand("go")
                    .WithDescription("Teletransporta al punto TP guardado")
                    .WithArgs(_api.ChatCommands.Parsers.Word("name"))
                    .HandleWith(OnAtpGo)
                .EndSubCommand()

                .BeginSubCommand("del")
                    .WithDescription("Elimina un punto TP guardado")
                    .WithArgs(_api.ChatCommands.Parsers.Word("name"))
                    .HandleWith(OnAtpDel)
                .EndSubCommand()

                .BeginSubCommand("list")
                    .WithDescription("Lista todos los puntos TP guardados")
                    .HandleWith(OnAtpList)
                .EndSubCommand()

                .BeginSubCommand("info")
                    .WithDescription("Muestra detalles de un punto TP guardado")
                    .WithArgs(_api.ChatCommands.Parsers.Word("name"))
                    .HandleWith(OnAtpInfo)
                .EndSubCommand();

            // ── /artp — random TP ─────────────────────────────────────
            _api.ChatCommands
                .Create("artp")
                .WithDescription("AxinMenuGUI — teletransporte aleatorio seguro")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    _api.ChatCommands.Parsers.OptionalWord("min"),
                    _api.ChatCommands.Parsers.OptionalWord("max"))
                .HandleWith(OnArtp);

            _api.Logger.Notification("[AxinMenuGUI] Comandos registrados: /amenu, /atp, /artp");
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

        // ═══ HANDLERS DE /atp ════════════════════════════════════════

        private TextCommandResult OnAtpSet(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Solo jugadores.");
            if (_adminTp == null) return TextCommandResult.Error("[AxinMenuGUI] AdminTeleportService no disponible.");

            var name = (string)args[0];
            var pos  = player.Entity.ServerPos;
            _adminTp.Set(name, pos.X, pos.Y, pos.Z, player.PlayerName);
            return TextCommandResult.Success(
                $"[AxinMenuGUI] Punto '{name}' guardado en ({pos.X:F0}, {pos.Y:F0}, {pos.Z:F0}).");
        }

        private TextCommandResult OnAtpSetAt(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Solo jugadores.");
            if (_adminTp == null) return TextCommandResult.Error("[AxinMenuGUI] AdminTeleportService no disponible.");

            var name = (string)args[0];
            var ci   = System.Globalization.CultureInfo.InvariantCulture;
            if (!double.TryParse((string)args[1], System.Globalization.NumberStyles.Any, ci, out double x)
             || !double.TryParse((string)args[2], System.Globalization.NumberStyles.Any, ci, out double y)
             || !double.TryParse((string)args[3], System.Globalization.NumberStyles.Any, ci, out double z))
                return TextCommandResult.Error("Uso: /atp setat <nombre> <x> <y> <z>");

            _adminTp.Set(name, x, y, z, player.PlayerName);
            return TextCommandResult.Success(
                $"[AxinMenuGUI] Punto '{name}' guardado en ({x:F0}, {y:F0}, {z:F0}).");
        }

        private TextCommandResult OnAtpGo(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Solo jugadores.");
            if (_adminTp == null) return TextCommandResult.Error("[AxinMenuGUI] AdminTeleportService no disponible.");

            var name = (string)args[0];
            bool ok  = _adminTp.TeleportTo(player, name);
            return ok
                ? TextCommandResult.Success()
                : TextCommandResult.Error($"[AxinMenuGUI] Punto '{name}' no encontrado. Usa /atp list.");
        }

        private TextCommandResult OnAtpDel(TextCommandCallingArgs args)
        {
            if (_adminTp == null) return TextCommandResult.Error("[AxinMenuGUI] AdminTeleportService no disponible.");

            var name = (string)args[0];
            bool ok  = _adminTp.Delete(name);
            return ok
                ? TextCommandResult.Success($"[AxinMenuGUI] Punto '{name}' eliminado.")
                : TextCommandResult.Error($"[AxinMenuGUI] Punto '{name}' no encontrado.");
        }

        private TextCommandResult OnAtpList(TextCommandCallingArgs args)
        {
            if (_adminTp == null) return TextCommandResult.Error("[AxinMenuGUI] AdminTeleportService no disponible.");

            var pts = _adminTp.GetAll();
            if (pts.Count == 0)
                return TextCommandResult.Success("[AxinMenuGUI] No hay puntos de TP guardados.");

            var sb = new StringBuilder();
            sb.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> Puntos de TP ({pts.Count}):");
            foreach (var pt in pts)
                sb.AppendLine(
                    $"  • <font color='#e9ddce'>{pt.Name}</font> " +
                    $"({pt.X:F0}, {pt.Y:F0}, {pt.Z:F0}) — por {pt.CreatedBy}");
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        private TextCommandResult OnAtpInfo(TextCommandCallingArgs args)
        {
            if (_adminTp == null) return TextCommandResult.Error("[AxinMenuGUI] AdminTeleportService no disponible.");

            var name = (string)args[0];
            var pt   = _adminTp.Get(name);
            if (pt == null) return TextCommandResult.Error($"[AxinMenuGUI] Punto '{name}' no encontrado.");

            var sb = new StringBuilder();
            sb.AppendLine($"<font color='gold'>[AxinMenuGUI]</font> TP Info: {pt.Name}");
            sb.AppendLine($"  X: {pt.X:F2}  Y: {pt.Y:F2}  Z: {pt.Z:F2}");
            sb.AppendLine($"  Creado por: {pt.CreatedBy}");
            sb.AppendLine($"  Fecha UTC:  {pt.CreatedAtUtc}");
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        // ═══ HANDLER DE /artp ════════════════════════════════════════

        private TextCommandResult OnArtp(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Solo jugadores.");
            if (_randomTp == null) return TextCommandResult.Error("[AxinMenuGUI] RandomTeleportService no disponible.");

            // Parsear min y max (aceptan enteros o sufijo k → *1000)
            var minStr = args[0] as string;
            var maxStr = args[1] as string;
            int min = 0, max = 0;

            if (!string.IsNullOrWhiteSpace(minStr))
            {
                min = ParseRadiusArg(minStr);
                if (min < 0)
                    return TextCommandResult.Error(
                        $"[AxinMenuGUI] min inválido: '{minStr}'. Usa un entero o sufijo k (ej. 5k).");
            }
            if (!string.IsNullOrWhiteSpace(maxStr))
            {
                max = ParseRadiusArg(maxStr);
                if (max < 0)
                    return TextCommandResult.Error(
                        $"[AxinMenuGUI] max inválido: '{maxStr}'. Usa un entero o sufijo k (ej. 10k).");
            }

            // 0 → usar defaults de config (RandomTeleportService los aplica)
            if (max > 0 && max <= min)
                return TextCommandResult.Error(
                    $"[AxinMenuGUI] max ({max}) debe ser mayor que min ({min}).");

            _randomTp.TeleportRandom(player, min, max);
            return TextCommandResult.Success();
        }

        // ─── Helpers de parseo ────────────────────────────────────────

        /// <summary>
        /// Parsea un argumento de radio: entero puro o con sufijo k (× 1000).
        /// Devuelve -1 si el formato es inválido.
        /// Ejemplos: "5000" → 5000, "5k" → 5000, "10K" → 10000.
        /// </summary>
        private static int ParseRadiusArg(string s)
        {
            s = s.Trim().ToLowerInvariant();
            if (s.EndsWith("k"))
            {
                if (int.TryParse(s[..^1], out int km)) return km * 1000;
                return -1;
            }
            return int.TryParse(s, out int v) ? v : -1;
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
