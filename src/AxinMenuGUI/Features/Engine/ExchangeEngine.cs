// AxinMenuGUI — Features/Engine
// Archivo: ExchangeEngine.cs
// Responsabilidad: operaciones de intercambio de ítems y variables.
//   buyItem, sellItem, giveItem, takeItem, setVariable.
// NO evalúa condiciones de menú. NO navega escenas. Solo manipula inventario y store.

using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    public class ExchangeEngine
    {
        private readonly ICoreServerAPI  _api;
        private readonly PlayerDataStore _store;

        public ExchangeEngine(ICoreServerAPI api, PlayerDataStore store)
        {
            _api   = api;
            _store = store;
        }

        // ═══ ENTRY POINTS — llamados desde MenuEngine ════════════════

        public void ExecuteExchange(IServerPlayer player, ClickEventDefinition ev, bool buy)
        {
            var costList = buy ? ev.Cost : ev.Give;
            var giveList = buy ? ev.Give : ev.Cost;
            var uid      = player.PlayerUID;

            // ── Verificar límites por jugador ──
            if (ev.Limits?.PerPlayer != null)
            {
                var limits   = ev.Limits.PerPlayer;
                var claimKey = $"exchange_{ev.Type}_{string.Join("_", giveList.Select(g => g.Item))}";

                if (limits.MaxTotal > 0)
                {
                    var claim = _store.GetClaim(uid, claimKey);
                    if (claim.Count >= limits.MaxTotal)
                    {
                        var msg = string.IsNullOrWhiteSpace(ev.ConditionFailMessage)
                            ? "[AxinMenuGUI] Has alcanzado el límite máximo de usos."
                            : ev.ConditionFailMessage;
                        player.SendMessage(0, msg, EnumChatType.Notification);
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(limits.Cooldown))
                {
                    var cooldownKey = $"cd_{claimKey}";
                    if (_store.IsCooldownActive(uid, cooldownKey))
                    {
                        var msg = string.IsNullOrWhiteSpace(ev.ConditionFailMessage)
                            ? "[AxinMenuGUI] Este artículo está en cooldown."
                            : ev.ConditionFailMessage;
                        player.SendMessage(0, msg, EnumChatType.Notification);
                        return;
                    }
                }
            }

            // ── Verificar que el jugador tiene el coste ──
            foreach (var cost in costList)
            {
                if (!HasItem(player, cost.Item, cost.Amount))
                {
                    var msg = string.IsNullOrWhiteSpace(ev.ConditionFailMessage)
                        ? $"[AxinMenuGUI] No tienes suficiente {cost.Item} (x{cost.Amount})."
                        : ev.ConditionFailMessage;
                    player.SendMessage(0, msg, EnumChatType.Notification);
                    return;
                }
            }

            // ── Consumir coste ──
            if (ev.Consume)
            {
                foreach (var cost in costList)
                    TakeItem(player, cost.Item, cost.Amount);
            }

            // ── Entregar ítems ──
            foreach (var give in giveList)
                GiveItem(player, give.Item, give.Amount);

            // ── Registrar claim y cooldown ──
            if (ev.Limits?.PerPlayer != null)
            {
                var claimKey = $"exchange_{ev.Type}_{string.Join("_", giveList.Select(g => g.Item))}";

                if (ev.Limits.PerPlayer.MaxTotal > 0)
                    _store.IncrementClaim(uid, claimKey);

                if (!string.IsNullOrWhiteSpace(ev.Limits.PerPlayer.Cooldown))
                    _store.SetCooldown(uid, $"cd_{claimKey}", ev.Limits.PerPlayer.Cooldown);
            }
        }

        public void GiveItem(IServerPlayer player, string itemCode, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            var loc   = new AssetLocation(itemCode);
            var item  = _api.World.GetItem(loc);
            ItemStack? stack = null;

            if (item != null)
                stack = new ItemStack(item, amount);
            else
            {
                var block = _api.World.GetBlock(loc);
                if (block != null) stack = new ItemStack(block, amount);
            }

            if (stack == null)
            {
                _api.Logger.Warning($"[AxinMenuGUI] giveItem: ítem '{itemCode}' no encontrado.");
                return;
            }

            if (!player.InventoryManager.TryGiveItemstack(stack, true))
            {
                // Inventario lleno — soltar al suelo
                _api.World.SpawnItemEntity(stack, player.Entity.ServerPos.XYZ);
            }
        }

        public void TakeItem(IServerPlayer player, string itemCode, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            int remaining = amount;
            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                foreach (var slot in inv)
                {
                    if (slot?.Itemstack?.Collectible?.Code?.ToString() != itemCode) continue;
                    int take = Math.Min(remaining, slot.Itemstack.StackSize);
                    slot.Itemstack.StackSize -= take;
                    if (slot.Itemstack.StackSize <= 0) slot.Itemstack = null;
                    slot.MarkDirty();
                    remaining -= take;
                    if (remaining <= 0) return;
                }
            }
        }

        public void ExecuteSetVariable(IServerPlayer player, ClickEventDefinition ev,
            string inputValue, RankingService? ranking = null)
        {
            if (string.IsNullOrWhiteSpace(ev.Variable)) return;
            var uid = player.PlayerUID;

            var resolvedValue = PlaceholderResolver.Resolve(ev.Value, player, _store, inputValue);

            if (ev.Operation == "set")
            {
                _store.Set(uid, player.PlayerName, ev.Variable, resolvedValue);
                return;
            }

            // Operaciones numéricas
            double current = double.TryParse(_store.Get(uid, ev.Variable), out double c) ? c : 0;
            double operand = double.TryParse(resolvedValue, out double o) ? o : 0;

            double result = ev.Operation switch
            {
                "add"      => current + operand,
                "subtract" => current - operand,
                "multiply" => current * operand,
                "divide"   => operand != 0 ? current / operand : current,
                _          => current
            };

            _store.Set(uid, player.PlayerName, ev.Variable, result.ToString());
        }

        // ═══ UTILIDAD — verificación de inventario ════════════════════
        // También usada por MenuEngine para la condición hasItem.

        public bool HasItem(IServerPlayer player, string itemCode, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return false;
            int total = 0;
            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                foreach (var slot in inv)
                {
                    if (slot?.Itemstack?.Collectible?.Code?.ToString() == itemCode)
                        total += slot.Itemstack.StackSize;
                    if (total >= amount) return true;
                }
            }
            return total >= amount;
        }
    }
}
