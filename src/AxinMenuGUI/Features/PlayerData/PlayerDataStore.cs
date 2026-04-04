// AxinMenuGUI — Features/PlayerData
// Archivo: PlayerDataStore.cs
// Responsabilidad: cargar, guardar y proveer acceso a los datos de jugador.
// Incluye: fields personalizados, stats automáticas, cooldowns, claims, quests.
// NO conoce menús ni click events. Solo persistencia.
//
// CAMBIO v0.5.5: KillsByType separado en HostileKillsByType + PassiveKillsByType.
// Migración automática via DocVersion: ficheros legacy se actualizan al cargar.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    // ═══════════════════════════════════════════════════════════
    // MODELOS DE DATOS
    // ═══════════════════════════════════════════════════════════

    public class PlayerStats
    {
        [JsonProperty("timeRealSeconds")]
        public double TimeRealSeconds { get; set; } = 0;

        [JsonProperty("timeGameDays")]
        public double TimeGameDays { get; set; } = 0;

        [JsonProperty("deaths")]
        public int Deaths { get; set; } = 0;

        [JsonProperty("mobKillsAll")]
        public int MobKillsAll { get; set; } = 0;

        [JsonProperty("mobKillsHostile")]
        public int MobKillsHostile { get; set; } = 0;

        [JsonProperty("playerKills")]
        public int PlayerKills { get; set; } = 0;

        /// <summary>Puntos acumulados por kills de mobs hostiles (configurables en config.json).</summary>
        [JsonProperty("mobKillsHostilePoints")]
        public double MobKillsHostilePoints { get; set; } = 0;

        /// <summary>
        /// Kills de mobs HOSTILES por tipo. Clave: entity.Code.Path normalizado.
        /// Solo incluye entidades en HostilePrefixes de PlayerStatsTracker.
        /// </summary>
        [JsonProperty("hostileKillsByType")]
        public Dictionary<string, int> HostileKillsByType { get; set; } = new();

        /// <summary>
        /// Kills de fauna NO HOSTIL por tipo. Clave: entity.Code.Path normalizado.
        /// Ciervos, cerdos, pollos, etc.
        /// </summary>
        [JsonProperty("passiveKillsByType")]
        public Dictionary<string, int> PassiveKillsByType { get; set; } = new();

        // Campo legacy — solo se lee del JSON antiguo; nunca se escribe.
        // Presente en ficheros sin DocVersion o con DocVersion < 0.1.
        [JsonProperty("killsByType")]
        public Dictionary<string, int>? KillsByTypeLegacy { get; set; } = null;
    }

    public class QuestEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("completedAt")]
        public string CompletedAt { get; set; } = "";
    }

    public class ClaimEntry
    {
        [JsonProperty("count")]
        public int Count { get; set; } = 0;

        [JsonProperty("lastAt")]
        public string LastAt { get; set; } = "";
    }

    public class PlayerData
    {
        // Versión del esquema de este fichero de jugador.
        // El store migra automáticamente si es inferior a PLAYERDATA_VERSION.
        [JsonProperty("DocVersion")]
        public double DocVersion { get; set; } = 0;

        [JsonProperty("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("playerName")]
        public string PlayerName { get; set; } = "";

        [JsonProperty("lastSeen")]
        public string LastSeen { get; set; } = "";

        [JsonProperty("stats")]
        public PlayerStats Stats { get; set; } = new();

        [JsonProperty("quests")]
        public PlayerQuests Quests { get; set; } = new();

        // Cooldowns: clave → ISO8601 de expiración
        [JsonProperty("cooldowns")]
        public Dictionary<string, string> Cooldowns { get; set; } = new();

        // Claims: claveÍtem → entrada con count y lastAt
        [JsonProperty("claims")]
        public Dictionary<string, ClaimEntry> Claims { get; set; } = new();

        // Fields: variables personalizadas admin
        [JsonProperty("fields")]
        public Dictionary<string, string> Fields { get; set; } = new();
    }

    public class PlayerQuests
    {
        [JsonProperty("active")]
        public List<string> Active { get; set; } = new();

        [JsonProperty("completed")]
        public List<QuestEntry> Completed { get; set; } = new();
    }

    // ═══════════════════════════════════════════════════════════
    // STORE
    // ═══════════════════════════════════════════════════════════

    public class PlayerDataStore : IDisposable
    {
        // Versión actual del esquema de playerdata.
        // Incrementar aquí cuando el modelo de PlayerData / PlayerStats cambie.
        private const double PLAYERDATA_VERSION = 0.1;

        private readonly ICoreServerAPI _api;
        private readonly Dictionary<string, PlayerData> _cache = new();

        private string DataFolder =>
            Path.Combine(_api.DataBasePath, "ModConfig", "AxinMenuGUI", "playerdata");

        public PlayerDataStore(ICoreServerAPI api)
        {
            _api = api;
            Directory.CreateDirectory(DataFolder);
        }

        // ─── Acceso básico ────────────────────────────────────────────

        public PlayerData GetOrCreate(string uid, string playerName = "")
        {
            if (_cache.TryGetValue(uid, out var cached))
            {
                if (!string.IsNullOrEmpty(playerName)) cached.PlayerName = playerName;
                return cached;
            }

            var path = FilePath(uid);
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonConvert.DeserializeObject<PlayerData>(json);
                    if (data != null)
                    {
                        if (!string.IsNullOrEmpty(playerName)) data.PlayerName = playerName;

                        // Migrar si la versión del fichero es anterior a la actual
                        bool migrated = MigrateIfNeeded(data);
                        _cache[uid] = data;

                        // Si hubo migración, persistir inmediatamente
                        if (migrated) Save(uid);

                        return data;
                    }
                }
                catch (Exception ex)
                {
                    _api.Logger.Warning(
                        $"[AxinMenuGUI] PlayerData corrupto para {uid}: {ex.Message}. Reiniciando.");
                }
            }

            var fresh = new PlayerData
            {
                Uid        = uid,
                PlayerName = playerName,
                DocVersion = PLAYERDATA_VERSION
            };
            _cache[uid] = fresh;
            return fresh;
        }

        public void Save(string uid)
        {
            if (!_cache.TryGetValue(uid, out var data)) return;
            try
            {
                data.LastSeen  = DateTime.UtcNow.ToString("o");
                data.DocVersion = PLAYERDATA_VERSION;

                // Nunca serializar el campo legacy
                data.Stats.KillsByTypeLegacy = null;

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(FilePath(uid), json);
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"[AxinMenuGUI] Error guardando PlayerData para {uid}: {ex.Message}");
            }
        }

        // ─── Migración por DocVersion ─────────────────────────────────
        // Devuelve true si se realizó alguna migración (para que el caller persista).
        // NORMA: nunca eliminar un bloque; solo añadir nuevos al final.

        private bool MigrateIfNeeded(PlayerData data)
        {
            if (data.DocVersion >= PLAYERDATA_VERSION) return false;

            _api.Logger.Notification(
                $"[AxinMenuGUI] Migrando playerdata de {data.PlayerName} " +
                $"v{data.DocVersion} → v{PLAYERDATA_VERSION}");

            if (data.DocVersion < 0.1)
            {
                // v0.0 → v0.1: killsByType unificado → hostileKillsByType + passiveKillsByType
                var legacy = data.Stats.KillsByTypeLegacy;
                if (legacy != null && legacy.Count > 0)
                {
                    foreach (var kv in legacy)
                    {
                        var dict = PlayerStatsTracker.IsHostileKey(kv.Key)
                            ? data.Stats.HostileKillsByType
                            : data.Stats.PassiveKillsByType;
                        if (!dict.ContainsKey(kv.Key)) dict[kv.Key] = 0;
                        dict[kv.Key] += kv.Value;
                    }
                    _api.Logger.Notification(
                        $"[AxinMenuGUI] Migración v0.1: {legacy.Count} kills distribuidos en hostile/passive.");
                }
                data.Stats.KillsByTypeLegacy = null;
            }

            // Plantilla para futuras versiones:
            // if (data.DocVersion < 0.2)
            // {
            //     // Cambios de v0.2 aquí
            // }

            return true;
        }

        // ─── Fields (variables personalizadas) ───────────────────────

        /// <summary>
        /// Obtiene el valor de un campo del jugador.
        /// Soporta prefijos de stats:
        ///   "stats.deaths", "stats.mobKillsAll", "stats.mobKillsHostile",
        ///   "stats.mobKillsHostilePoints", "stats.playerKills",
        ///   "stats.timeRealSeconds", "stats.timeGameDays"
        /// También soporta kills por tipo:
        ///   "stats.hostileKills.drifter-normal", "stats.passiveKills.pig"
        /// Para cualquier otro campo busca en Fields (variables personalizadas).
        /// </summary>
        public string Get(string uid, string field)
        {
            var data = GetOrCreate(uid);

            if (field.StartsWith("stats.", StringComparison.OrdinalIgnoreCase))
            {
                var key = field.Substring(6); // quitar "stats."
                var s   = data.Stats;

                // ── Stats escalares ──────────────────────────────────────
                switch (key.ToLowerInvariant())
                {
                    case "deaths":                 return s.Deaths.ToString();
                    case "mobkillsall":            return s.MobKillsAll.ToString();
                    case "mobkillshostile":        return s.MobKillsHostile.ToString();
                    case "mobkillshostilepoints":  return s.MobKillsHostilePoints.ToString("G");
                    case "playerkills":            return s.PlayerKills.ToString();
                    case "timerealseconds":        return s.TimeRealSeconds.ToString("G");
                    case "timegamedays":           return s.TimeGameDays.ToString("G");
                }

                // ── Kills por tipo: stats.hostileKills.<entityKey> ───────
                if (key.StartsWith("hostileKills.", StringComparison.OrdinalIgnoreCase))
                {
                    var entityKey = key.Substring(13);
                    s.HostileKillsByType.TryGetValue(entityKey, out int hkv);
                    return hkv.ToString();
                }
                if (key.StartsWith("passiveKills.", StringComparison.OrdinalIgnoreCase))
                {
                    var entityKey = key.Substring(13);
                    s.PassiveKillsByType.TryGetValue(entityKey, out int pkv);
                    return pkv.ToString();
                }

                // Stats no reconocida → vacío
                return "";
            }

            data.Fields.TryGetValue(field, out var val);
            return val ?? "";
        }

        public void Set(string uid, string playerName, string field, string value)
        {
            var data = GetOrCreate(uid, playerName);
            data.Fields[field] = value;
            Save(uid);
        }

        // ─── Cooldowns ────────────────────────────────────────────────

        public bool IsCooldownActive(string uid, string key)
        {
            var data = GetOrCreate(uid);
            if (!data.Cooldowns.TryGetValue(key, out var expiresStr)) return false;
            if (!DateTime.TryParse(expiresStr, out var expires)) return false;
            return DateTime.UtcNow < expires;
        }

        public void SetCooldown(string uid, string key, string duration)
        {
            var data = GetOrCreate(uid);
            var expires = DateTime.UtcNow.Add(ParseDuration(duration));
            data.Cooldowns[key] = expires.ToString("o");
            Save(uid);
        }

        public void ResetCooldowns(string uid)
        {
            var data = GetOrCreate(uid);
            data.Cooldowns.Clear();
            Save(uid);
        }

        // ─── Claims ───────────────────────────────────────────────────

        public ClaimEntry GetClaim(string uid, string key)
        {
            var data = GetOrCreate(uid);
            if (!data.Claims.TryGetValue(key, out var entry))
                return new ClaimEntry();
            return entry;
        }

        public void IncrementClaim(string uid, string key)
        {
            var data = GetOrCreate(uid);
            if (!data.Claims.ContainsKey(key))
                data.Claims[key] = new ClaimEntry();
            data.Claims[key].Count++;
            data.Claims[key].LastAt = DateTime.UtcNow.ToString("o");
            Save(uid);
        }

        public void ResetClaims(string uid)
        {
            var data = GetOrCreate(uid);
            data.Claims.Clear();
            Save(uid);
        }

        // ─── Stats ────────────────────────────────────────────────────

        public PlayerStats GetStats(string uid) => GetOrCreate(uid).Stats;

        public void ResetStats(string uid)
        {
            var data = GetOrCreate(uid);
            data.Stats = new PlayerStats();
            Save(uid);
        }

        // ─── Utilidades ───────────────────────────────────────────────

        /// <summary>Parsea duraciones: 30m, 12h, 7d, 1mo</summary>
        public static TimeSpan ParseDuration(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return TimeSpan.Zero;
            s = s.Trim().ToLowerInvariant();
            if (s.EndsWith("mo") && int.TryParse(s[..^2], out int mo))
                return TimeSpan.FromDays(mo * 30);
            if (s.EndsWith("d") && int.TryParse(s[..^1], out int d))
                return TimeSpan.FromDays(d);
            if (s.EndsWith("h") && int.TryParse(s[..^1], out int h))
                return TimeSpan.FromHours(h);
            if (s.EndsWith("m") && int.TryParse(s[..^1], out int m))
                return TimeSpan.FromMinutes(m);
            return TimeSpan.Zero;
        }

        private string FilePath(string uid)
        {
            var safe = uid.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            return Path.Combine(DataFolder, $"{safe}.json");
        }

        public void Dispose() { }
    }
}
