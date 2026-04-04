// AxinMenuGUI — Features/BlockClick
// Archivo: BlockClickHandler.cs
//
// ARQUITECTURA OPTIMIZADA — 4 niveles de filtrado:
//
//   Nivel 1 (cliente): el servidor envía BlockGuiListPacket con todas las pos
//     registradas. El cliente construye un índice por chunk. Al hacer clic
//     derecho, primero verifica si el chunk del bloque apuntado tiene bloques
//     GUI. Si no hay ninguno → no envía. Si hay → verifica distancia ≤ 6 → envía.
//
//   Nivel 2 (servidor): OnBlockRightClick solo loguea si hay pending O si
//     la pos tiene menú. Cero ruido en log para clics normales sin menú.
//
//   Nivel 3: lookup O(1) en BlockClickRegistry (Dictionary existente).
//
//   Nivel 4: el servidor reenvía BlockGuiListPacket al cliente cuando se
//     registra o borra un bloque, manteniendo el índice cliente sincronizado.

using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AxinMenuGUI
{
    // ── Packets ──────────────────────────────────────────────────────────

    [ProtoContract]
    public class BlockRightClickPacket
    {
        [ProtoMember(1)] public int X { get; set; }
        [ProtoMember(2)] public int Y { get; set; }
        [ProtoMember(3)] public int Z { get; set; }
    }

    /// <summary>
    /// Enviado servidor→cliente con la lista completa de bloques GUI registrados.
    /// El cliente lo usa para filtrar clics antes de enviar al servidor.
    /// </summary>
    [ProtoContract]
    public class BlockGuiListPacket
    {
        [ProtoMember(1)] public List<int> Xs { get; set; } = new();
        [ProtoMember(2)] public List<int> Ys { get; set; } = new();
        [ProtoMember(3)] public List<int> Zs { get; set; } = new();
    }

    // ── Server side ───────────────────────────────────────────────────────

    public class BlockClickHandlerServer
    {
        private readonly ICoreServerAPI        _api;
        private readonly BlockClickRegistry    _registry;
        private readonly MenuEngine            _engine;
        private readonly MenuRegistry          _menuRegistry;
        private readonly IServerNetworkChannel _channel;
        private readonly Dictionary<string, PendingState> _pending = new();
        private const double TimeoutSeconds = 60.0;

        public BlockClickHandlerServer(
            ICoreServerAPI api, BlockClickRegistry registry,
            MenuEngine engine, MenuRegistry menuRegistry,
            IServerNetworkChannel channel)
        {
            _api = api; _registry = registry;
            _engine = engine; _menuRegistry = menuRegistry;
            _channel = channel;
            _api.Event.RegisterGameTickListener(OnTick, 5000);
            // Enviar lista al cliente cuando conecta
            _api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        }

        private void OnPlayerNowPlaying(IServerPlayer player)
            => SendGuiList(player);

        /// <summary>Envía la lista de bloques GUI a un jugador (o null = todos).</summary>
        public void SendGuiList(IServerPlayer? player = null)
        {
            var pkt = _registry.BuildGuiListPacket();
            if (player != null)
                _channel.SendPacket(pkt, player);
            else
                _channel.BroadcastPacket(pkt);
        }

        public bool BeginPendingLink(IServerPlayer player, string menuId)
        {
            if (_menuRegistry.Get(menuId) == null) return false;
            _pending[player.PlayerUID] = new PendingState
            {
                Kind = PendingKind.Link, MenuId = menuId,
                ExpiresAt = DateTime.UtcNow.AddSeconds(TimeoutSeconds)
            };
            _api.Logger.Notification(
                $"[AxinMenuGUI] BlockClick: pending LINK {player.PlayerName} → '{menuId}'");
            return true;
        }

        public void BeginPendingDelete(IServerPlayer player)
        {
            _pending[player.PlayerUID] = new PendingState
            {
                Kind = PendingKind.Delete,
                ExpiresAt = DateTime.UtcNow.AddSeconds(TimeoutSeconds)
            };
        }

        public void OnBlockRightClick(IServerPlayer player, BlockRightClickPacket pkt)
        {
            var uid = player.PlayerUID;

            // ── Modo pending (link/delete) ────────────────────────────────
            if (_pending.TryGetValue(uid, out var state))
            {
                _pending.Remove(uid);
                if (state.Kind == PendingKind.Link)
                {
                    _registry.Register(pkt.X, pkt.Y, pkt.Z, state.MenuId!);
                    player.SendMessage(0,
                        $"[AxinMenuGUI] Menú '{state.MenuId}' registrado en bloque {pkt.X},{pkt.Y},{pkt.Z}",
                        EnumChatType.Notification);
                    _api.Logger.Notification(
                        $"[AxinMenuGUI] BlockClick: REGISTRADO '{state.MenuId}' en {pkt.X},{pkt.Y},{pkt.Z}");
                    // Sincronizar índice en todos los clientes
                    SendGuiList();
                }
                else
                {
                    bool deleted = _registry.Delete(pkt.X, pkt.Y, pkt.Z);
                    player.SendMessage(0,
                        deleted
                            ? $"[AxinMenuGUI] Bloque {pkt.X},{pkt.Y},{pkt.Z} desvinculado"
                            : "[AxinMenuGUI] No hay menú registrado en ese bloque",
                        EnumChatType.Notification);
                    if (deleted) SendGuiList();
                }
                return;
            }

            // ── Lookup normal: solo actuar si hay menú (sin log si no hay) ─
            var menuId = _registry.GetMenuId(new BlockPos(pkt.X, pkt.Y, pkt.Z));
            if (menuId == null) return;   // <── cero log, cero ruido

            _api.Logger.Notification(
                $"[AxinMenuGUI] BlockClick: abriendo '{menuId}' para {player.PlayerName}");
            _engine.OpenMenu(player, menuId);
        }

        private void OnTick(float dt)
        {
            var now = DateTime.UtcNow;
            var expired = new List<string>();
            foreach (var (uid, state) in _pending)
                if (now >= state.ExpiresAt) expired.Add(uid);
            foreach (var uid in expired)
            {
                _pending.Remove(uid);
                (_api.World.PlayerByUid(uid) as IServerPlayer)?.SendMessage(0,
                    "[AxinMenuGUI] Modo vinculación expirado (60s). Usa /amenu click-open de nuevo.",
                    EnumChatType.Notification);
            }
        }

        public void Dispose()
        {
            _api.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
        }
    }

    // ── Client side ───────────────────────────────────────────────────────

    public class BlockClickHandlerClient
    {
        private readonly ICoreClientAPI _capi;
        private IClientNetworkChannel?  _channel;

        // Índice espacial por chunk (chunkX,chunkZ) → lista de BlockPos con GUI
        // Chunk = bloque >> 5 (32 bloques por chunk en VS)
        private const int ChunkShift = 5;
        private const int InteractRadius = 6; // bloques, distancia Manhattan

        private readonly Dictionary<long, List<BlockPos>> _guiByChunk = new();

        public BlockClickHandlerClient(ICoreClientAPI capi) { _capi = capi; }

        public void Init(IClientNetworkChannel channel)
        {
            _channel = channel;
            _channel.SetMessageHandler<BlockGuiListPacket>(OnGuiListReceived);
            _capi.Event.MouseDown += OnMouseDown;
            _capi.Logger.Notification("[AxinMenuGUI] BlockClickHandlerClient: activo.");
        }

        private void OnGuiListReceived(BlockGuiListPacket pkt)
        {
            _guiByChunk.Clear();
            int count = pkt.Xs.Count;
            for (int i = 0; i < count; i++)
            {
                var pos      = new BlockPos(pkt.Xs[i], pkt.Ys[i], pkt.Zs[i]);
                long chunkKey = ChunkKey(pkt.Xs[i], pkt.Zs[i]);
                if (!_guiByChunk.TryGetValue(chunkKey, out var list))
                    _guiByChunk[chunkKey] = list = new List<BlockPos>();
                list.Add(pos);
            }
            _capi.Logger.VerboseDebug(
                $"[AxinMenuGUI] BlockClick: índice actualizado, {count} bloque(s) GUI.");
        }

        private void OnMouseDown(MouseEvent evt)
        {
            if (evt.Button != EnumMouseButton.Right) return;
            if (!_capi.Input.MouseGrabbed) return;

            var sel = _capi.World.Player?.CurrentBlockSelection;
            if (sel?.Position == null) return;

            var pos = sel.Position;

            // Nivel 1: ¿el chunk del bloque apuntado tiene algún GUI registrado?
            long ck = ChunkKey(pos.X, pos.Z);
            if (!_guiByChunk.TryGetValue(ck, out var candidates) || candidates.Count == 0)
                return;

            // Nivel 2: ¿algún GUI registrado está dentro del radio de interacción?
            bool near = false;
            foreach (var gp in candidates)
            {
                int d = Math.Abs(gp.X - pos.X) + Math.Abs(gp.Y - pos.Y) + Math.Abs(gp.Z - pos.Z);
                if (d <= InteractRadius) { near = true; break; }
            }
            if (!near) return;

            _channel?.SendPacket(new BlockRightClickPacket
            {
                X = pos.X, Y = pos.Y, Z = pos.Z
            });
        }

        private static long ChunkKey(int bx, int bz)
        {
            int cx = bx >> ChunkShift;
            int cz = bz >> ChunkShift;
            return ((long)cx << 32) | (uint)cz;
        }
    }

    internal enum PendingKind { Link, Delete }
    internal class PendingState
    {
        public PendingKind Kind { get; set; }
        public string?     MenuId    { get; set; }
        public DateTime    ExpiresAt { get; set; }
    }
}
