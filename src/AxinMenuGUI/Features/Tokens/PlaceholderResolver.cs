// AxinMenuGUI — Features/Tokens
// Archivo: PlaceholderResolver.cs
// Responsabilidad: resolver tokens en strings de menú.
//
// Tokens disponibles:
//   {player}                    → nombre del jugador
//   {uid}                       → UID del jugador
//   {pos}                       → posición X,Y,Z
//   {world}                     → nombre del mundo
//   {gamemode}                  → modo de juego
//   {input}                     → input ChatFetcher (v3)
//   {var:campo}                 → field personalizado del jugador
//   {stats.deaths}              → número de muertes
//   {stats.mobKillsAll}         → total mobs asesinados
//   {stats.mobKillsHostile}     → mobs hostiles asesinados
//   {stats.mobKillsHostilePoints} → puntos acumulados por kills hostiles
//   {stats.playerKills}         → jugadores asesinados
//   {stats.timeReal}            → tiempo real formateado (Xh Ym)
//   {stats.timeGame}            → tiempo in-game formateado (X.X días)
//   {ranking.position}          → posición en el ranking del jugador
//   {ranking.value}             → valor del campo de ranking del jugador
//   {ranking.top}               → ranking completo formateado (para chat/menú)
//   {ranking.top3}              → top 3 formateado

using System;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public static class PlaceholderResolver
    {
        private static readonly Regex VarPattern =
            new(@"\{var:([^}]+)\}", RegexOptions.Compiled);

        private static readonly Regex RankingTopPattern =
            new(@"\{ranking\.top(\d+)?\}", RegexOptions.Compiled);

        public static string Resolve(
            string text,
            IServerPlayer player,
            PlayerDataStore store,
            string inputValue = "",
            RankingService? ranking = null)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Tokens básicos
            text = text.Replace("{player}",   player.PlayerName);
            text = text.Replace("{uid}",       player.PlayerUID);
            text = text.Replace("{input}",     inputValue);

            var pos = player.Entity?.Pos;
            if (pos != null)
                text = text.Replace("{pos}", $"{(int)pos.X},{(int)pos.Y},{(int)pos.Z}");

            text = text.Replace("{world}",
                player.Entity?.World?.Config?.GetString("worldname") ?? "");
            text = text.Replace("{gamemode}",
                player.WorldData?.CurrentGameMode.ToString() ?? "");

            // Tokens stats
            var stats = store.GetStats(player.PlayerUID);
            text = text.Replace("{stats.deaths}",
                stats.Deaths.ToString());
            text = text.Replace("{stats.mobKillsAll}",
                stats.MobKillsAll.ToString());
            text = text.Replace("{stats.mobKillsHostile}",
                stats.MobKillsHostile.ToString());
            text = text.Replace("{stats.mobKillsHostilePoints}",
                $"{stats.MobKillsHostilePoints:F1}");
            text = text.Replace("{stats.playerKills}",
                stats.PlayerKills.ToString());
            text = text.Replace("{stats.timeReal}",
                FormatTimeReal(stats.TimeRealSeconds));
            text = text.Replace("{stats.timeGame}",
                FormatTimeGame(stats.TimeGameDays));

            // Tokens ranking
            if (ranking != null)
            {
                text = text.Replace("{ranking.position}",
                    ranking.GetPlayerPosition(player.PlayerName).ToString());
                text = text.Replace("{ranking.value}",
                    $"{ranking.GetPlayerRankingValue(player.PlayerUID):F1}");

                // {ranking.top} → ranking completo
                text = text.Replace("{ranking.top}", ranking.FormatRanking());

                // {ranking.top3}, {ranking.top5}, etc.
                text = RankingTopPattern.Replace(text, m =>
                {
                    int n = m.Groups[1].Success
                        ? int.Parse(m.Groups[1].Value)
                        : 10;
                    return ranking.FormatRanking(n);
                });
            }

            // {var:campo}
            text = VarPattern.Replace(text, m =>
                store.Get(player.PlayerUID, m.Groups[1].Value));

            return text;
        }

        public static string ResolveBasic(string text, IServerPlayer player)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("{player}", player.PlayerName);
            text = text.Replace("{uid}",    player.PlayerUID);
            return text;
        }

        private static string FormatTimeReal(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }

        private static string FormatTimeGame(double days)
        {
            if (days >= 1) return $"{days:F1} días";
            return $"{days * 24:F1}h";
        }
    }
}
