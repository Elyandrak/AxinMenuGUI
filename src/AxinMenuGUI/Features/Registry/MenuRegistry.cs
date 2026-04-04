// AxinMenuGUI — Features/Registry
// Archivo: MenuRegistry.cs
// v0.6.7: menús embebidos con prioridad inferior al usuario.
//
// REGLA: Los ficheros de usuario (no-embebido) se cargan PRIMERO.
// Los embebidos solo se cargan si su ID no está ya registrado.
// RegisterAliases en CommandHandler ya respeta el orden de carga:
// el alias del menú de usuario (cargado primero) se registra,
// y cuando llega el alias del embebido ya está en _registeredAliases.
//
// EXTRACCIÓN: los embebidos solo se extraen si el fichero NO existe en disco.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class MenuRegistry : IDisposable
    {
        private readonly ICoreServerAPI _api;
        private readonly Dictionary<string, MenuDefinition> _menus = new();

        private string MenusFolder =>
            Path.Combine(_api.DataBasePath, "ModConfig", "AxinMenuGUI", "menus");

        private static readonly HashSet<string> EmbeddedIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "ejemplo", "ejemplo_tienda", "ejemplo_admin",
            "ejemplo_multitema", "ejemplo_jugador", "ejemplo_stats", "ejemplo_temas"
        };

        private static readonly Dictionary<string, string> EmbeddedMenus = new()
        {
            ["AxinMenuGUI.DefaultMenus.ejemplo.json"]           = "ejemplo.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_tienda.json"]    = "ejemplo_tienda.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_admin.json"]     = "ejemplo_admin.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_multitema.json"] = "ejemplo_multitema.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_jugador.json"]   = "ejemplo_jugador.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_stats.json"]     = "ejemplo_stats.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_temas.json"]     = "ejemplo_temas.json",
        };

        public int Count => _menus.Count;
        public MenuRegistry(ICoreServerAPI api) { _api = api; }

        public void LoadAll()
        {
            _menus.Clear();
            Directory.CreateDirectory(MenusFolder);
            ExtractEmbeddedMenus();

            var all = Directory.GetFiles(MenusFolder, "*.json");
            // Separar: primero los del usuario, luego los embebidos
            var userFiles    = all.Where(f => !IsEmbeddedFile(f)).OrderBy(f => f).ToList();
            var embeddFiles  = all.Where(f =>  IsEmbeddedFile(f)).OrderBy(f => f).ToList();

            int loaded = 0, skipped = 0;
            foreach (var file in userFiles.Concat(embeddFiles))
                LoadFile(file, ref loaded, ref skipped);

            _api.Logger.Notification(
                $"[AxinMenuGUI] Menús cargados: {loaded} OK, {skipped} omitidos.");
        }

        private void LoadFile(string file, ref int loaded, ref int skipped)
        {
            try
            {
                var menu = JsonConvert.DeserializeObject<MenuDefinition>(File.ReadAllText(file));
                if (menu == null || string.IsNullOrWhiteSpace(menu.Id))
                {
                    _api.Logger.Warning($"[AxinMenuGUI] {Path.GetFileName(file)}: sin 'id' o inválido.");
                    skipped++; return;
                }
                if (_menus.ContainsKey(menu.Id))
                {
                    // Si es embebido y el usuario tiene ese ID → OK silencioso
                    if (!EmbeddedIds.Contains(menu.Id))
                        _api.Logger.Warning($"[AxinMenuGUI] ID duplicado '{menu.Id}'. Ignorado.");
                    skipped++; return;
                }
                _menus[menu.Id] = menu;
                loaded++;
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"[AxinMenuGUI] Error cargando {Path.GetFileName(file)}: {ex.Message}");
                skipped++;
            }
        }

        private void ExtractEmbeddedMenus()
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var (res, fileName) in EmbeddedMenus)
            {
                var dest = Path.Combine(MenusFolder, fileName);
                if (File.Exists(dest)) continue; // nunca sobreescribir
                using var stream = asm.GetManifestResourceStream(res);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                File.WriteAllText(dest, reader.ReadToEnd());
            }
        }

        private static bool IsEmbeddedFile(string path) =>
            EmbeddedIds.Contains(Path.GetFileNameWithoutExtension(path));

        public MenuDefinition? Get(string id) { _menus.TryGetValue(id, out var m); return m; }
        public IEnumerable<string> GetAllIds() => _menus.Keys;
        public IEnumerable<MenuDefinition> GetAll() => _menus.Values;
        public void Dispose() { }
    }
}
