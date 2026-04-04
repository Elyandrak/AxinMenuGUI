// AxinMenuGUI — Features/Teleport
// Archivo: RandomTeleportService.cs
// Responsabilidad: teletransporte aleatorio seguro dentro de un anillo [min, max]
//   centrado en el DefaultSpawnPosition del mundo.
//
// Algoritmo:
//   1. Genera candidatos (x, z) dentro del anillo con ángulo y radio aleatorios.
//   2. Rechaza candidatos dentro de storyZoneMargin del spawn (proxy de zona historia).
//   3. Rechaza candidatos dentro de claims/protecciones (claimMargin bloques).
//   4. Valida suelo seguro con SafeLocationFinder.TryGetSafeY().
//   5. Teleporta al primer candidato válido.
//   6. Falla limpiamente tras maxAttempts intentos.

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class RandomTeleportService
    {
        private readonly ICoreServerAPI _api;
        private readonly ConfigManager  _config;
        private readonly Random         _rng = new();

        public RandomTeleportService(ICoreServerAPI api, ConfigManager config)
        {
            _api    = api;
            _config = config;
        }

        /// <summary>
        /// Intenta teletransportar al jugador a un punto aleatorio seguro dentro
        /// del anillo [min, max] centrado en el spawn del mundo.
        ///
        /// Si min &lt;= 0 usa config.DefaultMin. Si max &lt;= 0 usa config.DefaultMax.
        /// Envía mensajes al jugador con el resultado.
        /// Devuelve true si el TP fue exitoso.
        /// </summary>
        public bool TeleportRandom(IServerPlayer player, int min, int max)
        {
            var cfg = _config.Config.RandomTeleport;

            if (min <= 0) min = cfg.DefaultMin;
            if (max <= 0) max = cfg.DefaultMax;

            if (min <= 0)
            {
                player.SendMessage(0,
                    "[AxinMenuGUI] RTP: min debe ser mayor que 0.",
                    EnumChatType.Notification);
                return false;
            }
            if (max <= min)
            {
                player.SendMessage(0,
                    $"[AxinMenuGUI] RTP: max ({max}) debe ser mayor que min ({min}).",
                    EnumChatType.Notification);
                return false;
            }

            var    spawn       = _api.World.DefaultSpawnPosition;
            double spawnX      = spawn.X;
            double spawnZ      = spawn.Z;
            int    maxAttempts = Math.Max(1, cfg.MaxAttempts);
            var    ba          = _api.World.BlockAccessor;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Generar candidato en anillo [min, max] con distribución uniforme de ángulo.
                // Radio uniforme en [min, max] (no uniforme de área, pero aceptable para TP).
                double angle  = _rng.NextDouble() * 2.0 * Math.PI;
                double radius = _rng.NextDouble() * (max - min) + min;
                int    cx     = (int)(spawnX + radius * Math.Cos(angle));
                int    cz     = (int)(spawnZ + radius * Math.Sin(angle));

                // Rechazo por proximidad al spawn (proxy de zona de historia)
                if (SafeLocationFinder.IsNearStoryZone(_api, cx, cz, cfg.StoryZoneMargin))
                    continue;

                // Obtener Y aproximada para el check de protección
                int roughY = ba.GetRainMapHeightAt(cx, cz);

                // Rechazo por protección/claim
                if (SafeLocationFinder.IsLocationProtected(_api, cx, roughY, cz, cfg.ClaimMargin))
                    continue;

                // Validar suelo seguro
                if (!SafeLocationFinder.TryGetSafeY(ba, cx, cz, cfg, out int safeY))
                    continue;

                // TP exitoso — centrar en el bloque (+0.5 en X y Z)
                AdminTeleportService.TeleportPlayerTo(player, cx + 0.5, safeY, cz + 0.5);
                player.SendMessage(0,
                    $"[AxinMenuGUI] Teletransportado a ({cx}, {safeY}, {cz}) " +
                    $"tras {attempt} intento(s).",
                    EnumChatType.Notification);
                _api.Logger.Notification(
                    $"[AxinMenuGUI] RTP {player.PlayerName}: ({cx}, {safeY}, {cz}) " +
                    $"radio={radius:F0} intentos={attempt}");
                return true;
            }

            player.SendMessage(0,
                $"[AxinMenuGUI] RTP: no se encontró ubicación segura en " +
                $"{maxAttempts} intentos. Intenta de nuevo o contacta a un admin.",
                EnumChatType.Notification);
            _api.Logger.Warning(
                $"[AxinMenuGUI] RTP {player.PlayerName}: agotados {maxAttempts} intentos " +
                $"(min={min}, max={max}).");
            return false;
        }
    }
}
