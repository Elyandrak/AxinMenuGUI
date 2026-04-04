// AxinMenuGUI — Features/Teleport
// Archivo: SafeLocationFinder.cs
// Responsabilidad: validar si una posición del terreno es segura para teletransportar
//   a un jugador. Usado por RandomTeleportService.
//
// LIMITACIONES DOCUMENTADAS:
//   Claims: usa api.World.Claims (API vanilla de VS). Si el servidor usa un mod de
//     claims de terceros, esta comprobación puede no detectar todas las protecciones.
//     Fallback: si la API lanza excepción o no está disponible, se permite la ubicación.
//
//   Story zones: no existe una API de runtime en VS para consultar la ubicación real
//     de las zonas de historia (ruinas, structures, etc.). La comprobación usa
//     distancia mínima al DefaultSpawnPosition del mundo como proxy conservador.
//     Configurable en config.json → randomTeleport.storyZoneMargin.

using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public static class SafeLocationFinder
    {
        /// <summary>
        /// Intenta encontrar la Y segura para colocar los pies de un jugador
        /// en la columna (x, z). Usa el rain map como punto de partida y
        /// escanea una ventana estrecha alrededor de él.
        ///
        /// Criterios de seguridad:
        ///   - Bloque de suelo (Y-1): sólido, no aire, no líquido.
        ///   - Bloque en pies (Y): aire.
        ///   - Bloque en cabeza (Y+1): aire (si requireTwoAirBlocks=true).
        /// </summary>
        /// <param name="ba">IBlockAccessor del servidor.</param>
        /// <param name="x">Coordenada X.</param>
        /// <param name="z">Coordenada Z.</param>
        /// <param name="cfg">Config de RTP (ForbidWater, RequireTwoAirBlocks).</param>
        /// <param name="playerY">Y resultante donde colocar los pies del jugador.</param>
        /// <returns>true si se encontró posición segura.</returns>
        public static bool TryGetSafeY(
            IBlockAccessor ba,
            int x,
            int z,
            RandomTeleportConfig cfg,
            out int playerY)
        {
            playerY = 0;

            // GetRainMapHeightAt devuelve en VS el Y del primer bloque de aire
            // por encima del suelo (i.e., suelo en rainY-1, pies del jugador en rainY).
            // Escaneamos ±3 bloques para cubrir posibles diferencias entre versiones.
            int rainY = ba.GetRainMapHeightAt(x, z);
            if (rainY <= 1 || rainY >= 490) return false;

            int scanMax = Math.Min(490, rainY + 3);
            int scanMin = Math.Max(2,   rainY - 3);

            for (int testY = scanMax; testY >= scanMin; testY--)
            {
                // Bloque de suelo: testY - 1
                var ground = ba.GetBlock(new BlockPos(x, testY - 1, z));
                if (ground == null || ground.BlockId == 0) continue;    // aire → no válido
                if (ground.IsLiquid())
                {
                    if (cfg.ForbidWater) return false;  // agua → rechaza columna entera
                    continue;
                }

                // Bloque en pies del jugador: testY
                var feet = ba.GetBlock(new BlockPos(x, testY, z));
                if (feet == null || feet.BlockId != 0) continue;        // no es aire

                // Bloque en cabeza del jugador: testY + 1
                if (cfg.RequireTwoAirBlocks)
                {
                    var head = ba.GetBlock(new BlockPos(x, testY + 1, z));
                    if (head == null || head.BlockId != 0) continue;    // no es aire
                }

                playerY = testY;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Comprueba si la posición está dentro de una protección/claim VS vanilla.
        /// Usa api.World.Claims.Get(Cuboidd area).
        ///
        /// LIMITACIÓN: solo detecta claims del sistema vanilla de VS.
        /// Mods de claims externos (p. ej. LandClaims mod) no son comprobados aquí.
        /// Si la API lanza excepción, devuelve false (permite la ubicación).
        /// </summary>
        public static bool IsLocationProtected(ICoreServerAPI api, int x, int y, int z, int margin)
        {
            if (margin <= 0) return false;
            try
            {
                double m    = Math.Max(1, margin);
                var area    = new Cuboidd(x - m, y - m, z - m, x + m, y + m, z + m);
                var claims  = api.World.Claims.Get(area);
                return claims != null && claims.Length > 0;
            }
            catch
            {
                // API no disponible o retorno inesperado → permitir
                return false;
            }
        }

        /// <summary>
        /// Comprueba si la posición está demasiado cerca del DefaultSpawnPosition del mundo.
        ///
        /// LIMITACIÓN: no hay API de runtime en VS para consultar las zonas de historia
        /// reales. Este método usa la distancia al spawn como proxy conservador, ya que
        /// las estructuras de narrativa suelen generarse en el área de spawn inicial.
        ///
        /// Si margin &lt;= 0, el check se deshabilita (siempre devuelve false).
        /// </summary>
        public static bool IsNearStoryZone(ICoreServerAPI api, int x, int z, int margin)
        {
            if (margin <= 0) return false;
            try
            {
                var spawn = api.World.DefaultSpawnPosition;
                double dx = x - spawn.X;
                double dz = z - spawn.Z;
                return Math.Sqrt(dx * dx + dz * dz) < margin;
            }
            catch
            {
                return false;
            }
        }
    }
}
