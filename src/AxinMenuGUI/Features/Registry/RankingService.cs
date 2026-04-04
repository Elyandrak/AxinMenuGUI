// AxinMenuGUI — Features/Ranking
// Archivo: RankingService.cs
// Responsabilidad: calcular y cachear el ranking de jugadores.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class RankingEntry
    {
        public int    Position   { get; set; }
        public string PlayerName { get; set; } = "";
        public double Value      { get; set; }
        public string Field      { get; set; } = "";
    }

    public class RankingService
    {
        private readonly ICoreServerAPI _api;
        private readonly ConfigManager  _config;

        private List<RankingEntry> _cache        = new();
        private DateTime           _lastCalculated = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        private string DataFolder =>
            Path.Combine(_api.DataBasePath, "ModConfig", "AxinMenuGUI", "playerdata");

        public RankingService(ICoreServerAPI api, ConfigManager config)
        {
            _api    = api;
            _config = config;
        }

        public List<RankingEntry> GetRanking(bool forceRefresh = false)
        {
            if (!forceRefresh && DateTime.UtcNow - _lastCalculated < CacheDuration)
                return _cache;
            return Calculate();
        }

        private List<RankingEntry> Calculate()
        {
            var field   = _config.Config.RankingField;
            var size    = _config.Config.RankingSize;
            var entries = new List<(string name, double value)>();

            if (!Directory.Exists(DataFolder)) { _cache = new(); return _cache; }

            foreach (var file in Directory.GetFiles(DataFolder, "*.json"))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<PlayerData>(File.ReadAllText(file));
                    if (data == null) continue;
                    entries.Add((data.PlayerName, GetFieldValue(data, field)));
                }
                catch { }
            }

            _cache = entries
                .OrderByDescending(e => e.value)
                .Take(size)
                .Select((e, i) => new RankingEntry
                {
                    Position   = i + 1,
                    PlayerName = e.name,
                    Value      = e.value,
                    Field      = field
                })
                .ToList();

            _lastCalculated = DateTime.UtcNow;
            return _cache;
        }

        public int GetPlayerPosition(string playerName)
        {
            var entry = GetRanking().FirstOrDefault(e =>
                e.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            return entry?.Position ?? -1;
        }

        public double GetPlayerRankingValue(string uid)
        {
            var safe = uid.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            var path = Path.Combine(DataFolder, $"{safe}.json");
            if (!File.Exists(path)) return 0;
            try
            {
                var data = JsonConvert.DeserializeObject<PlayerData>(File.ReadAllText(path));
                return data == null ? 0 : GetFieldValue(data, _config.Config.RankingField);
            }
            catch { return 0; }
        }

        public string FormatRanking(int maxEntries = 0)
        {
            var ranking = GetRanking();
            if (maxEntries > 0) ranking = ranking.Take(maxEntries).ToList();
            if (ranking.Count == 0) return "No hay datos de ranking aún.";

            var field = _config.Config.RankingField;
            var sb    = new StringBuilder();
            sb.AppendLine($"<font color='gold'>── Ranking: {field} ──</font>");
            foreach (var e in ranking)
            {
                var val = field.Contains("Point") || field.Contains("Day")
                    ? $"{e.Value:F1}" : $"{(int)e.Value}";
                sb.AppendLine($"  <font color='silver'>#{e.Position}</font> " +
                              $"<font color='cyan'>{e.PlayerName}</font> — {val}");
            }
            return sb.ToString().TrimEnd();
        }

        public static double GetFieldValue(PlayerData data, string field) => field switch
        {
            "mobKillsHostilePoints" => data.Stats.MobKillsHostilePoints,
            "mobKillsAll"           => data.Stats.MobKillsAll,
            "mobKillsHostile"       => data.Stats.MobKillsHostile,
            "playerKills"           => data.Stats.PlayerKills,
            "deaths"                => data.Stats.Deaths,
            "timeRealSeconds"       => data.Stats.TimeRealSeconds,
            "timeGameDays"          => data.Stats.TimeGameDays,
            _                       => 0
        };

        public void InvalidateCache() => _lastCalculated = DateTime.MinValue;
    }
}
