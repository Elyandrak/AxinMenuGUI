// AxinMenuGUI — Features/PlayerData
// Archivo: PlayerDataStore.cs
// Responsabilidad: guardar y leer variables por jugador en ficheros JSON individuales.
// NO conoce menús ni click events. Solo persistencia.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class PlayerData
    {
        [JsonProperty("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("playerName")]
        public string PlayerName { get; set; } = "";

        [JsonProperty("fields")]
        public Dictionary<string, string> Fields { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────
    public class PlayerDataStore : IDisposable
    {
        private readonly ICoreServerAPI _api;

        // Cache en memoria (uid → datos). Se escribe a disco en cada Set().
        private readonly Dictionary<string, PlayerData> _cache = new();

        private string DataFolder =>
            Path.Combine(_api.DataBasePath, "ModConfig", "AxinMenuGUI", "playerdata");

        // ─────────────────────────────────────────────────────────────
        public PlayerDataStore(ICoreServerAPI api)
        {
            _api = api;
            Directory.CreateDirectory(DataFolder);
        }

        // ─────────────────────────────────────────────────────────────
        /// <summary>Devuelve el valor de un campo, o "" si no existe.</summary>
        public string Get(string uid, string field)
        {
            var data = LoadOrCreate(uid);
            data.Fields.TryGetValue(field, out var val);
            return val ?? "";
        }

        // ─────────────────────────────────────────────────────────────
        /// <summary>Guarda un valor para un campo del jugador y persiste en disco.</summary>
        public void Set(string uid, string playerName, string field, string value)
        {
            var data = LoadOrCreate(uid);
            data.PlayerName   = playerName;
            data.Fields[field] = value;
            Save(uid, data);
        }

        // ─────────────────────────────────────────────────────────────
        private PlayerData LoadOrCreate(string uid)
        {
            if (_cache.TryGetValue(uid, out var cached))
                return cached;

            var path = FilePath(uid);
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonConvert.DeserializeObject<PlayerData>(json);
                    if (data != null)
                    {
                        _cache[uid] = data;
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    _api.Logger.Warning(
                        $"[AxinMenuGUI] PlayerData corrupto para {uid}: {ex.Message}. Reiniciando.");
                }
            }

            var fresh = new PlayerData { Uid = uid };
            _cache[uid] = fresh;
            return fresh;
        }

        // ─────────────────────────────────────────────────────────────
        private void Save(string uid, PlayerData data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(FilePath(uid), json);
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"[AxinMenuGUI] Error guardando PlayerData para {uid}: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        /// <summary>Sanitiza el UID para usarlo como nombre de fichero.</summary>
        private string FilePath(string uid)
        {
            // Sustituir caracteres no válidos en nombre de fichero
            var safe = uid.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            return Path.Combine(DataFolder, $"{safe}.json");
        }

        // ─────────────────────────────────────────────────────────────
        public void Dispose() { }
    }
}
