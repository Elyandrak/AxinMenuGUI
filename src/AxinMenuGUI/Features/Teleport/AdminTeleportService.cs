// AxinMenuGUI — Features/Teleport
// Archivo: AdminTeleportService.cs
// Responsabilidad: cargar, guardar y gestionar los puntos de TP admin.
//   Ejecuta el teletransporte hacia un punto guardado.
// Persistencia: ModConfig/AxinMenuGUI/adminteleports.json

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class AdminTeleportService
    {
        private readonly ICoreServerAPI    _api;
        private AdminTeleportStoreData     _data = new();

        private string FilePath =>
            Path.Combine(_api.DataBasePath, "ModConfig", "AxinMenuGUI", "adminteleports.json");

        public AdminTeleportService(ICoreServerAPI api)
        {
            _api = api;
            Load();
        }

        // ── Persistencia ─────────────────────────────────────────────

        private void Load()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

                if (!File.Exists(FilePath))
                {
                    _data = new AdminTeleportStoreData();
                    Save();
                    return;
                }

                var json = File.ReadAllText(FilePath);
                _data = JsonConvert.DeserializeObject<AdminTeleportStoreData>(json) ?? new();
                _api.Logger.Notification(
                    $"[AxinMenuGUI] AdminTeleports cargados: {_data.Points.Count} punto(s).");
            }
            catch (Exception ex)
            {
                _api.Logger.Warning(
                    $"[AxinMenuGUI] Error cargando adminteleports.json: {ex.Message}. Usando vacío.");
                _data = new AdminTeleportStoreData();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                _api.Logger.Error($"[AxinMenuGUI] Error guardando adminteleports.json: {ex.Message}");
            }
        }

        // ── CRUD ─────────────────────────────────────────────────────

        /// <summary>
        /// Guarda (o sobreescribe) un punto con el nombre dado.
        /// La comparación de nombre es case-insensitive.
        /// </summary>
        public void Set(string name, double x, double y, double z, string createdBy)
        {
            var normalized = name.Trim();
            var existing   = FindPoint(normalized);
            if (existing != null)
                _data.Points.Remove(existing);

            _data.Points.Add(new AdminTeleportPoint
            {
                Name         = normalized,
                X            = x,
                Y            = y,
                Z            = z,
                CreatedBy    = createdBy,
                CreatedAtUtc = DateTime.UtcNow.ToString("O")
            });
            Save();
        }

        /// <summary>Devuelve el punto por nombre (case-insensitive) o null si no existe.</summary>
        public AdminTeleportPoint? Get(string name)
            => FindPoint(name.Trim());

        /// <summary>Elimina un punto. Devuelve true si existía y fue eliminado.</summary>
        public bool Delete(string name)
        {
            var pt = FindPoint(name.Trim());
            if (pt == null) return false;
            _data.Points.Remove(pt);
            Save();
            return true;
        }

        /// <summary>Lista todos los puntos guardados.</summary>
        public System.Collections.Generic.List<AdminTeleportPoint> GetAll()
            => _data.Points;

        // ── Teletransporte ───────────────────────────────────────────

        /// <summary>
        /// Teletransporta a un jugador al punto guardado con ese nombre.
        /// Envía mensajes al jugador. Devuelve false si el punto no existe.
        /// </summary>
        public bool TeleportTo(IServerPlayer player, string name)
        {
            var pt = Get(name);
            if (pt == null)
            {
                player.SendMessage(0,
                    $"[AxinMenuGUI] Punto de TP '{name}' no encontrado.",
                    EnumChatType.Notification);
                return false;
            }

            TeleportPlayerTo(player, pt.X, pt.Y, pt.Z);
            player.SendMessage(0,
                $"[AxinMenuGUI] Teletransportado a '{pt.Name}' " +
                $"({pt.X:F0}, {pt.Y:F0}, {pt.Z:F0}).",
                EnumChatType.Notification);
            return true;
        }

        /// <summary>Teletransporta al jugador a las coordenadas absolutas dadas.</summary>
        public static void TeleportPlayerTo(IServerPlayer player, double x, double y, double z)
        {
            player.Entity.TeleportTo(new Vec3d(x, y, z));
        }

        // ── Helpers ──────────────────────────────────────────────────

        private AdminTeleportPoint? FindPoint(string normalizedName)
            => _data.Points.FirstOrDefault(p =>
                p.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
    }
}
