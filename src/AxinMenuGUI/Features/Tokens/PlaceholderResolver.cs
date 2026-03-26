// AxinMenuGUI — Features/Tokens
// Archivo: PlaceholderResolver.cs
// Responsabilidad: resolver tokens {player}, {uid}, {var:campo}, etc. en strings.
// NO conoce menús ni GUI. Solo sustitución de texto.

using System;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public static class PlaceholderResolver
    {
        // Patrón: {var:nombreCampo}
        private static readonly Regex VarPattern = new(@"\{var:([^}]+)\}", RegexOptions.Compiled);

        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Resuelve todos los tokens en un string.
        /// Tokens desconocidos se dejan como literal (no crashea).
        /// </summary>
        public static string Resolve(
            string text,
            IServerPlayer player,
            PlayerDataStore store,
            string inputValue = "")
        {
            if (string.IsNullOrEmpty(text)) return text;

            // {player}
            text = text.Replace("{player}", player.PlayerName);

            // {uid}
            text = text.Replace("{uid}", player.PlayerUID);

            // {input}  (usado por ChatFetcher, v3)
            text = text.Replace("{input}", inputValue);

            // {pos}
            var pos = player.Entity?.Pos;
            if (pos != null)
            {
                var posStr = $"{(int)pos.X},{(int)pos.Y},{(int)pos.Z}";
                text = text.Replace("{pos}", posStr);
            }

            // {world}
            text = text.Replace("{world}", player.Entity?.World?.Config?.GetString("worldname") ?? "");

            // {gamemode}
            text = text.Replace("{gamemode}", player.WorldData?.CurrentGameMode.ToString() ?? "");

            // {var:campo}
            text = VarPattern.Replace(text, match =>
            {
                var field = match.Groups[1].Value;
                return store.Get(player.PlayerUID, field);
            });

            return text;
        }

        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Versión sin PlayerDataStore (para contextos donde solo se necesitan
        /// tokens básicos de jugador).
        /// </summary>
        public static string ResolveBasic(string text, IServerPlayer player)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("{player}", player.PlayerName);
            text = text.Replace("{uid}", player.PlayerUID);
            return text;
        }
    }
}
