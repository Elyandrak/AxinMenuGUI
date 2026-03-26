// AxinMenuGUI — Features/Registry
// Archivo: MenuRegistry.cs
// Responsabilidad: cargar, parsear y proveer acceso a los menús definidos en JSON.
// NO ejecuta eventos. NO renderiza. Solo carga y almacena.
//
// Menús de ejemplo embebidos en el DLL:
//   - Si falta algún menú de ejemplo en disco → se extrae automáticamente.
//   - NUNCA sobreescribe un JSON que ya existe en disco.

using System;
using System.Collections.Generic;
using System.IO;
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

        // Recurso embebido → nombre de archivo destino en disco
        private static readonly Dictionary<string, string> EmbeddedMenus = new()
        {
            ["AxinMenuGUI.DefaultMenus.ejemplo.json"]        = "ejemplo.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_tienda.json"] = "ejemplo_tienda.json",
            ["AxinMenuGUI.DefaultMenus.ejemplo_admin.json"]  = "ejemplo_admin.json",
        };

        public int Count => _menus.Count;

        public MenuRegistry(ICoreServerAPI api)
        {
            _api = api;
        }

        /// <summary>
        /// Carga (o recarga) todos los ficheros .json de la carpeta menus/.
        /// Garantiza que los menús de ejemplo están en disco antes de cargar.
        /// </summary>
        public void LoadAll()
        {
            _menus.Clear();

            Directory.CreateDirectory(MenusFolder);
            ExtractEmbeddedMenus();

            var files = Directory.GetFiles(MenusFolder, "*.json");

            int loaded = 0;
            int failed = 0;

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var menu = JsonConvert.DeserializeObject<MenuDefinition>(json);

                    if (menu == null || string.IsNullOrWhiteSpace(menu.Id))
                    {
                        _api.Logger.Warning(
                            $"[AxinMenuGUI] {Path.GetFileName(file)}: ignorado (sin 'id' o JSON inválido).");
                        failed++;
                        continue;
                    }

                    if (_menus.ContainsKey(menu.Id))
                    {
                        _api.Logger.Warning(
                            $"[AxinMenuGUI] ID duplicado '{menu.Id}' en {Path.GetFileName(file)}. Ignorado.");
                        failed++;
                        continue;
                    }

                    _menus[menu.Id] = menu;
                    loaded++;
                }
                catch (Exception ex)
                {
                    _api.Logger.Error(
                        $"[AxinMenuGUI] Error al cargar {Path.GetFileName(file)}: {ex.Message}");
                    failed++;
                }
            }

            _api.Logger.Notification(
                $"[AxinMenuGUI] Menús cargados: {loaded} OK, {failed} con error.");
        }

        /// <summary>
        /// Extrae los menús embebidos en el DLL al disco.
        /// Solo escribe si el archivo NO existe — nunca sobreescribe.
        /// </summary>
        private void ExtractEmbeddedMenus()
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var (resourceName, fileName) in EmbeddedMenus)
            {
                var destPath = Path.Combine(MenusFolder, fileName);
                if (File.Exists(destPath)) continue;

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _api.Logger.Warning(
                        $"[AxinMenuGUI] Recurso embebido no encontrado: {resourceName}");
                    continue;
                }

                using var reader = new StreamReader(stream);
                File.WriteAllText(destPath, reader.ReadToEnd());

                _api.Logger.Notification(
                    $"[AxinMenuGUI] Menú de ejemplo repuesto: menus/{fileName}");
            }
        }

        public MenuDefinition? Get(string id)
        {
            _menus.TryGetValue(id, out var menu);
            return menu;
        }

        public IEnumerable<string> GetAllIds() => _menus.Keys;
        public IEnumerable<MenuDefinition> GetAll() => _menus.Values;

        public void Dispose() { }
    }
}
