// AxinMenuGUI — Features/Config
// Archivo: ModConfig.cs
// Responsabilidad: cargar y proveer la configuración global del mod.
// Fichero: ModConfig/AxinMenuGUI/config.json

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    // ═══════════════════════════════════════════════════════════
    // CONFIGURACIÓN DE RANDOM TP
    // ═══════════════════════════════════════════════════════════

    public class RandomTeleportConfig
    {
        /// <summary>Radio mínimo por defecto desde el spawn (en bloques). Usado si /artp no da args.</summary>
        [JsonProperty("defaultMin")]
        public int DefaultMin { get; set; } = 5000;

        /// <summary>Radio máximo por defecto desde el spawn (en bloques). Usado si /artp no da args.</summary>
        [JsonProperty("defaultMax")]
        public int DefaultMax { get; set; } = 10000;

        /// <summary>Número máximo de intentos antes de fallar.</summary>
        [JsonProperty("maxAttempts")]
        public int MaxAttempts { get; set; } = 40;

        /// <summary>
        /// Margen en bloques alrededor del candidato que se comprueba contra claims VS vanilla.
        /// 0 = deshabilitar comprobación de claims.
        /// </summary>
        [JsonProperty("claimMargin")]
        public int ClaimMargin { get; set; } = 128;

        /// <summary>
        /// Distancia mínima al DefaultSpawnPosition del mundo (proxy de zona de historia).
        /// 0 = deshabilitar. Ver SafeLocationFinder.IsNearStoryZone para limitaciones.
        /// </summary>
        [JsonProperty("storyZoneMargin")]
        public int StoryZoneMargin { get; set; } = 256;

        /// <summary>Si true, rechaza candidatos cuyo bloque de suelo sea líquido.</summary>
        [JsonProperty("forbidWater")]
        public bool ForbidWater { get; set; } = true;

        /// <summary>Si true, exige 2 bloques de aire (pies + cabeza) sobre el suelo.</summary>
        [JsonProperty("requireTwoAirBlocks")]
        public bool RequireTwoAirBlocks { get; set; } = true;
    }

    // ═══════════════════════════════════════════════════════════
    // CONFIGURACIÓN GLOBAL DEL MOD
    // ═══════════════════════════════════════════════════════════

    public class AxinMenuConfig
    {
        // Versión del esquema de config.json.
        // El mod compara este valor con CONFIG_VERSION y migra si es inferior.
        [JsonProperty("DocVersion")]
        public double DocVersion { get; set; } = 0;

        /// <summary>Idioma para mensajes del mod. Códigos: en, es, fr, de, pt-br, ru</summary>
        [JsonProperty("language")]
        public string Language { get; set; } = "en";

        /// <summary>Puntos por kill de mob hostil. Clave: entity.Code.Path normalizado.</summary>
        [JsonProperty("killPoints")]
        public Dictionary<string, double> KillPoints { get; set; } = new()
        {
            // ── Drifters ──────────────────────────────────────────────
            ["drifter-normal"]         = 0.5,
            ["drifter-deep"]           = 2.0,
            ["drifter-tainted"]        = 4.0,
            ["drifter-corrupt"]        = 8.0,
            ["drifter-nightmare"]      = 14.0,
            ["drifter-double-headed"]  = 20.0,
            // ── Arañas ────────────────────────────────────────────────
            ["spider-normal"]          = 1.0,
            ["spider-poison"]          = 3.0,
            // ── Langostas ─────────────────────────────────────────────
            ["locust-normal"]          = 0.5,
            ["locust-corrupt"]         = 2.0,
            ["locust-sawblade"]        = 4.0,
            // ── Bells of the Deep ─────────────────────────────────────
            ["bell-normal"]            = 5.0,
            // ── Nightmare ─────────────────────────────────────────────
            ["nightmare"]              = 12.0,
            // ── Lobos ─────────────────────────────────────────────────
            ["wolf-male"]              = 3.0,
            ["wolf-female"]            = 2.5,
            // ── Osos ──────────────────────────────────────────────────
            ["bear-black"]             = 5.0,
            ["bear-brown"]             = 6.0,
            ["bear-polar"]             = 8.0,
            // ── Hienas ────────────────────────────────────────────────
            ["hyena"]                  = 2.5,
            // ── Leones ────────────────────────────────────────────────
            ["lion-male"]              = 7.0,
            ["lion-female"]            = 5.0,
            // ── Leopardos ─────────────────────────────────────────────
            ["leopard"]                = 4.0,
            // ── Cocodrilos ────────────────────────────────────────────
            ["crocodile"]              = 5.0,
            // ── Serpientes ────────────────────────────────────────────
            ["snake-normal"]           = 1.0,
            ["snake-poison"]           = 2.0,
            // ── Tiburones ─────────────────────────────────────────────
            ["shark"]                  = 8.0,
            // ── Cangrejos ─────────────────────────────────────────────
            ["crab"]                   = 1.5,
            ["hermitcrab"]             = 1.0,
            ["rivercrab"]              = 1.0,
        };

        /// <summary>Número de posiciones en el ranking público.</summary>
        [JsonProperty("rankingSize")]
        public int RankingSize { get; set; } = 10;

        /// <summary>Campo usado para ordenar el ranking: mobKillsHostilePoints | mobKillsAll | deaths | timeRealSeconds</summary>
        [JsonProperty("rankingField")]
        public string RankingField { get; set; } = "mobKillsHostilePoints";

        /// <summary>Configuración del sistema de teletransporte aleatorio (/artp).</summary>
        [JsonProperty("randomTeleport")]
        public RandomTeleportConfig RandomTeleport { get; set; } = new();
    }

    public class ConfigManager
    {
        // Versión actual del esquema. Incrementar aquí cuando se añadan campos nuevos.
        // El fichero en disco se migrará automáticamente si su DocVersion es inferior.
        private const double CONFIG_VERSION = 0.3;

        private readonly ICoreServerAPI _api;
        private AxinMenuConfig _config = new();

        private string ConfigPath =>
            Path.Combine(_api.DataBasePath, "ModConfig", "AxinMenuGUI", "config.json");

        public AxinMenuConfig Config => _config;

        public ConfigManager(ICoreServerAPI api)
        {
            _api = api;
            Load();
        }

        public void Load()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

            if (!File.Exists(ConfigPath))
            {
                _config = new AxinMenuConfig();
                _config.DocVersion = CONFIG_VERSION;
                Save();
                _api.Logger.Notification(
                    "[AxinMenuGUI] Config creada en ModConfig/AxinMenuGUI/config.json");
                return;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                _config = JsonConvert.DeserializeObject<AxinMenuConfig>(json) ?? new();

                // ── Migración automática ───────────────────────────────────
                if (_config.DocVersion < CONFIG_VERSION)
                {
                    Migrate(_config.DocVersion);
                    _config.DocVersion = CONFIG_VERSION;
                    Save();
                }

                _api.Logger.Notification(
                    $"[AxinMenuGUI] Config cargada v{_config.DocVersion}. " +
                    $"Idioma: {_config.Language} | killPoints: {_config.KillPoints.Count} tipos");
            }
            catch (Exception ex)
            {
                _api.Logger.Warning(
                    $"[AxinMenuGUI] Error cargando config.json: {ex.Message}. Usando valores por defecto.");
                _config = new AxinMenuConfig();
                _config.DocVersion = CONFIG_VERSION;
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"[AxinMenuGUI] Error guardando config.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Devuelve los puntos para una criatura dado su path completo de VS.
        /// Usa lookup progresivo: prueba la clave completa, luego sin el último segmento,
        /// etc., hasta encontrar coincidencia en KillPoints o devolver 0.
        /// Esto cubre paths con sufijos de variante como "wolf-male-adult" → "wolf-male".
        /// </summary>
        public double GetKillPoints(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return 0;
            var parts = rawPath.Split('-');
            for (int len = parts.Length; len > 0; len--)
            {
                var key = string.Join("-", parts, 0, len);
                if (_config.KillPoints.TryGetValue(key, out double pts))
                    return pts;
            }
            return 0;
        }

        // ── Lógica de migración por versión ───────────────────────────
        // Cada bloque "case" acumula los cambios desde esa versión hasta la siguiente.
        // NORMA: nunca eliminar un case; solo añadir nuevos al final.

        private void Migrate(double fromVersion)
        {
            _api.Logger.Notification(
                $"[AxinMenuGUI] Migrando config.json v{fromVersion} → v{CONFIG_VERSION}");

            if (fromVersion < 0.1)
            {
                // v0.0 → v0.1: no había DocVersion. Nada que migrar estructuralmente.
            }

            if (fromVersion < 0.2)
            {
                // v0.1 → v0.2: se añaden todos los mobs hostiles no-drifter.
                // Los killPoints del jugador se conservan; solo se añaden las claves ausentes.
                var defaults = new AxinMenuConfig().KillPoints;
                int added = 0;
                foreach (var kv in defaults)
                {
                    if (!_config.KillPoints.ContainsKey(kv.Key))
                    {
                        _config.KillPoints[kv.Key] = kv.Value;
                        added++;
                    }
                }
                _api.Logger.Notification(
                    $"[AxinMenuGUI] Migración v0.2: {added} entrada(s) de killPoints añadida(s).");
            }

            if (fromVersion < 0.3)
            {
                // v0.2 → v0.3: se añade el bloque randomTeleport.
                // Newtonsoft.Json ya inicializa RandomTeleport con defaults si no estaba en disco.
                // La migración solo es necesaria para garantizar que el fichero se guarda
                // con el nuevo bloque visible.
                _api.Logger.Notification(
                    "[AxinMenuGUI] Migración v0.3: bloque 'randomTeleport' añadido con valores por defecto.");
            }
        }
    }
}
