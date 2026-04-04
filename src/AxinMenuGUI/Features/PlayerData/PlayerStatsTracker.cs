// AxinMenuGUI — Features/PlayerData
// Archivo: PlayerStatsTracker.cs
// Responsabilidad: recolectar métricas automáticas de jugadores mediante hooks VS.
// Registra: tiempo real, tiempo in-game, muertes, mobs asesinados (todos + hostiles),
//           jugadores asesinados, kills por tipo de criatura (separados hostile/passive).
//
// CAMBIO v0.5.5:
//   - Ampliados HostilePrefixes con todos los mobs hostiles de VS 1.21.
//   - IsHostileKey() expuesto como public static (usado en PlayerDataStore para migración).
//   - RegisterKill escribe en HostileKillsByType o PassiveKillsByType según hostilidad.
//   - Los playerKills ya NO se escriben en KillsByType (solo en PlayerKills counter).

using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class PlayerStatsTracker : IDisposable
    {
        private readonly ICoreServerAPI  _api;
        private readonly PlayerDataStore _store;
        private readonly ConfigManager   _config;

        private readonly Dictionary<string, DateTime> _joinTimes    = new();
        private readonly Dictionary<string, double>   _joinGameDays = new();

        // ── Lista blanca de prefijos de criaturas hostiles en VS 1.21 ─
        // Comparación contra entity.Code.Path (sin dominio, sin variante numérica).
        private static readonly HashSet<string> HostilePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "drifter",
            "spider",
            "locust",
            "bell",
            "nightmare",
            "wolf",
            "bear",
            "boar",
            "hyena",
            "lion",
            "leopard",
            "crocodile",
            "snake",
            "shark",
            "crab",
            "hermitcrab",
            "rivercrab",
        };

        // ── API pública — usada por PlayerDataStore para migración legacy ──
        /// <summary>
        /// Devuelve true si la clave normalizada corresponde a un mob hostil.
        /// Necesario para MigrateLegacyKills en PlayerDataStore.
        /// </summary>
        public static bool IsHostileKey(string normalizedKey)
        {
            if (string.IsNullOrEmpty(normalizedKey)) return false;
            foreach (var prefix in HostilePrefixes)
            {
                if (normalizedKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ── Traducciones para i18n ─────────────────────────────────────
        private static readonly Dictionary<string, string> CreatureTranslationKeys =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // Drifters
            { "drifter-normal",         "creature-drifter-normal"         },
            { "drifter-deep",           "creature-drifter-deep"           },
            { "drifter-tainted",        "creature-drifter-tainted"        },
            { "drifter-corrupt",        "creature-drifter-corrupt"        },
            { "drifter-nightmare",      "creature-drifter-nightmare"      },
            { "drifter-double-headed",  "creature-drifter-double-headed"  },
            // Arañas
            { "spider-normal",          "creature-spider-normal"          },
            { "spider-poison",          "creature-spider-poison"          },
            // Langostas
            { "locust-normal",          "creature-locust-normal"          },
            { "locust-corrupt",         "creature-locust-corrupt"         },
            { "locust-sawblade",        "creature-locust-sawblade"        },
            // Bells
            { "bell-normal",            "creature-bell-normal"            },
            // Nightmare
            { "nightmare",              "creature-nightmare"              },
            // Lobos
            { "wolf-male",              "creature-wolf-male"              },
            { "wolf-female",            "creature-wolf-female"            },
            // Osos
            { "bear-black",             "creature-bear-black"             },
            { "bear-brown",             "creature-bear-brown"             },
            { "bear-polar",             "creature-bear-polar"             },
            // Hienas
            { "hyena",                  "creature-hyena"                  },
            // Leones
            { "lion-male",              "creature-lion-male"              },
            { "lion-female",            "creature-lion-female"            },
            // Leopardos
            { "leopard",                "creature-leopard"                },
            // Cocodrilos
            { "crocodile",              "creature-crocodile"              },
            // Serpientes
            { "snake-normal",           "creature-snake-normal"           },
            { "snake-poison",           "creature-snake-poison"           },
            // Tiburones
            { "shark",                  "creature-shark"                  },
            // Cangrejos
            { "crab",                   "creature-crab"                   },
            { "hermitcrab",             "creature-hermitcrab"             },
            { "rivercrab",              "creature-rivercrab"              },
            // Jabalíes
            { "boar",                   "creature-boar"                   },
            // Fauna pasiva
            { "hare",                   "creature-hare"                   },
            { "fox",                    "creature-fox"                    },
            { "raccoon",                "creature-raccoon"                },
            { "chicken-rooster",        "creature-chicken-rooster"        },
            { "chicken-hen",            "creature-chicken-hen"            },
            { "pig",                    "creature-pig"                    },
            { "sheep",                  "creature-sheep"                  },
            { "goat",                   "creature-goat"                   },
            { "deer",                   "creature-deer"                   },
            { "gazelle",                "creature-gazelle"                },
            { "moose",                  "creature-moose"                  },
            { "elephant",               "creature-elephant"               },
            { "ostrich",                "creature-ostrich"                },
        };

        // ─────────────────────────────────────────────────────────────
        public PlayerStatsTracker(ICoreServerAPI api, PlayerDataStore store, ConfigManager config)
        {
            _api    = api;
            _store  = store;
            _config = config;

            api.Event.PlayerJoin       += OnPlayerJoin;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.PlayerDeath      += OnPlayerDeath;
            api.Event.OnEntityDeath    += OnEntityDeath;
        }

        // ─── Join ─────────────────────────────────────────────────────

        private void OnPlayerJoin(IServerPlayer player)
        {
            var uid = player.PlayerUID;
            _store.GetOrCreate(uid, player.PlayerName);
            _joinTimes[uid]    = DateTime.UtcNow;
            _joinGameDays[uid] = _api.World.Calendar?.TotalDays ?? 0;
            _api.Logger.Notification(
                $"[AxinMenuGUI] PlayerStats: {player.PlayerName} conectado.");
        }

        // ─── Disconnect ───────────────────────────────────────────────

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            var uid  = player.PlayerUID;
            var data = _store.GetOrCreate(uid, player.PlayerName);

            if (_joinTimes.TryGetValue(uid, out var joinTime))
            {
                data.Stats.TimeRealSeconds += (DateTime.UtcNow - joinTime).TotalSeconds;
                _joinTimes.Remove(uid);
            }

            if (_joinGameDays.TryGetValue(uid, out var joinDays))
            {
                var currentDays = _api.World.Calendar?.TotalDays ?? joinDays;
                data.Stats.TimeGameDays += Math.Max(0, currentDays - joinDays);
                _joinGameDays.Remove(uid);
            }

            _store.Save(uid);
        }

        // ─── Muerte del jugador ───────────────────────────────────────

        private void OnPlayerDeath(IServerPlayer player, DamageSource? damageSource)
        {
            var data = _store.GetOrCreate(player.PlayerUID, player.PlayerName);
            data.Stats.Deaths++;
            _store.Save(player.PlayerUID);
        }

        // ─── Muerte de entidad ────────────────────────────────────────

        private void OnEntityDeath(Entity entity, DamageSource? damageSource)
        {
            if (damageSource?.GetCauseEntity() is not EntityPlayer killer) return;

            var player = _api.World.PlayerByUid(killer.PlayerUID);
            if (player is not IServerPlayer serverPlayer) return;

            var uid  = serverPlayer.PlayerUID;
            var data = _store.GetOrCreate(uid, serverPlayer.PlayerName);

            // ── Jugador (PvP) ─────────────────────────────────────────
            if (entity is EntityPlayer)
            {
                data.Stats.PlayerKills++;
                data.Stats.MobKillsAll++;
                // Los kills de jugadores no se clasifican por tipo de mob
                _store.Save(uid);
                return;
            }

            // ── Mob ───────────────────────────────────────────────────
            data.Stats.MobKillsAll++;

            var path    = entity.Code?.Path ?? "";
            var typeKey = NormalizeCreatureKey(path);
            bool isHostile = IsHostileByPath(path);

            if (isHostile)
            {
                data.Stats.MobKillsHostile++;

                // Acumular puntos: se pasa el path RAW para que GetKillPoints
                // haga lookup progresivo y cubra sufijos de variante/edad/género.
                // Ejemplos: "wolf-male-adult" → resuelve "wolf-male" = 3.0
                //           "locust-corrupt-sawblade" → resuelve "locust-corrupt" = 2.0
                double pts = _config.GetKillPoints(path);
                if (pts > 0) data.Stats.MobKillsHostilePoints += pts;

                // typeKey normalizado para el diccionario (más limpio para mostrar)
                RegisterKill(data.Stats.HostileKillsByType, typeKey);
            }
            else
            {
                // Registrar en lista de fauna pasiva
                RegisterKill(data.Stats.PassiveKillsByType, typeKey);
            }

            _store.Save(uid);
        }

        // ─── Helpers privados ─────────────────────────────────────────

        private static bool IsHostileByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var prefix in HostilePrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string NormalizeCreatureKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return "unknown";
            var parts = path.Split('-');
            if (parts.Length > 1 && int.TryParse(parts[^1], out _))
                return string.Join("-", parts[..^1]);
            return path;
        }

        private static void RegisterKill(Dictionary<string, int> dict, string typeKey)
        {
            if (!dict.ContainsKey(typeKey))
                dict[typeKey] = 0;
            dict[typeKey]++;
        }

        // ─── SaveAllOnline ────────────────────────────────────────────

        public void SaveAllOnline()
        {
            foreach (var player in _api.Server.Players)
            {
                if (player.ConnectionState != EnumClientState.Playing) continue;
                OnPlayerDisconnect(player);
                _joinTimes[player.PlayerUID]    = DateTime.UtcNow;
                _joinGameDays[player.PlayerUID] = _api.World.Calendar?.TotalDays ?? 0;
            }
        }

        public void Dispose()
        {
            _api.Event.PlayerJoin       -= OnPlayerJoin;
            _api.Event.PlayerDisconnect -= OnPlayerDisconnect;
            _api.Event.PlayerDeath      -= OnPlayerDeath;
            _api.Event.OnEntityDeath    -= OnEntityDeath;
        }
    }
}
