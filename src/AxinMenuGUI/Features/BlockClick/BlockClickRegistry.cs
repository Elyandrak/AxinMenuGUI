// AxinMenuGUI — Features/BlockClick
// Archivo: BlockClickRegistry.cs
// v0.6.7: eliminado campo dim (VS no tiene dimensiones)

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class BlockClickEntry
    {
        [JsonProperty("menuId")] public string MenuId { get; set; } = "";
        [JsonProperty("x")]      public int    X      { get; set; }
        [JsonProperty("y")]      public int    Y      { get; set; }
        [JsonProperty("z")]      public int    Z      { get; set; }
    }

    public class BlockClickRegistry
    {
        private readonly ICoreServerAPI _api;
        private readonly string _filePath;
        private const string FileVersion = "1.1.0";
        private readonly Dictionary<string, string> _map = new();

        public BlockClickRegistry(ICoreServerAPI api)
        {
            _api = api;
            var folder = Path.Combine(api.DataBasePath, "ModConfig", "AxinMenuGUI");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "menusClick.json");
            if (!File.Exists(_filePath)) WriteFile(new List<BlockClickEntry>());
            Load();
        }

        public string? GetMenuId(BlockPos pos)
        {
            _map.TryGetValue(Key(pos.X, pos.Y, pos.Z), out var id);
            return id;
        }

        public void Register(int x, int y, int z, string menuId)
        {
            _map[Key(x, y, z)] = menuId;
            Save();
        }

        public bool Delete(int x, int y, int z)
        {
            var k = Key(x, y, z);
            if (!_map.ContainsKey(k)) return false;
            _map.Remove(k);
            Save();
            return true;
        }

        private void Load()
        {
            _map.Clear();
            try
            {
                var json  = File.ReadAllText(_filePath);
                var token = JToken.Parse(json);
                List<BlockClickEntry>? entries = null;

                if (token is JObject root && root["entries"] is JArray arr)
                    entries = arr.ToObject<List<BlockClickEntry>>();
                else if (token is JArray directArr)
                {
                    entries = directArr.ToObject<List<BlockClickEntry>>();
                    Save(); // migra al nuevo formato
                }

                if (entries == null) return;
                foreach (var e in entries)
                    if (!string.IsNullOrWhiteSpace(e.MenuId))
                        _map[Key(e.X, e.Y, e.Z)] = e.MenuId;

                _api.Logger.Notification(
                    $"[AxinMenuGUI] BlockClickRegistry: {_map.Count} bloque(s) vinculado(s).");
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"[AxinMenuGUI] BlockClickRegistry: error al leer — {ex.Message}");
            }
        }

        private void Save()
        {
            var entries = new List<BlockClickEntry>();
            foreach (var (k, menuId) in _map)
            {
                var p = k.Split(',');
                if (p.Length != 3) continue;
                entries.Add(new BlockClickEntry
                {
                    MenuId = menuId,
                    X = int.Parse(p[0]), Y = int.Parse(p[1]), Z = int.Parse(p[2])
                });
            }
            try { WriteFile(entries); }
            catch (Exception ex)
            {
                _api.Logger.Error($"[AxinMenuGUI] BlockClickRegistry: error al guardar — {ex.Message}");
            }
        }

        private void WriteFile(List<BlockClickEntry> entries)
        {
            var root = new JObject
            {
                ["_version"] = FileVersion,
                ["_comment"] = "AxinMenuGUI — Block Click Registry. Usa /amenu click-open y /amenu click-delete.",
                ["_fields"]  = new JObject
                {
                    ["menuId"] = "ID del menú (debe existir en menus/)",
                    ["x"] = "Coordenada X", ["y"] = "Coordenada Y", ["z"] = "Coordenada Z"
                },
                ["entries"] = JArray.FromObject(entries)
            };
            File.WriteAllText(_filePath, root.ToString(Formatting.Indented));
        }

        /// <summary>
        /// Construye un packet con la lista de todos los bloques GUI registrados.
        /// Usado por BlockClickHandlerServer para sincronizar el índice cliente.
        /// </summary>
        public BlockGuiListPacket BuildGuiListPacket()
        {
            var pkt = new BlockGuiListPacket();
            foreach (var (k, _) in _map)
            {
                var p = k.Split(',');
                if (p.Length != 3) continue;
                pkt.Xs.Add(int.Parse(p[0]));
                pkt.Ys.Add(int.Parse(p[1]));
                pkt.Zs.Add(int.Parse(p[2]));
            }
            return pkt;
        }

        private static string Key(int x, int y, int z) => $"{x},{y},{z}";
    }
}
