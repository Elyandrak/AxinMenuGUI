// AxinMenuGUI — Core
// Archivo: AxinMenuGuiMod.cs
// Responsabilidad: punto de entrada del mod, arranque de subsistemas.

using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace AxinMenuGUI
{
    public class AxinMenuGuiMod : ModSystem
    {
        // CRÍTICO: sin esto VS solo carga el ModSystem en el servidor
        // aunque modinfo.json diga "side": "Universal"
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        // ── Server-side ──────────────────────────────────────────
        private MenuRegistry?    _registry;
        private MenuEngine?      _engine;
        private PlayerDataStore? _playerData;
        private CommandHandler?  _commands;
        private IServerNetworkChannel? _serverChannel;

        // ── Client-side ──────────────────────────────────────────
        private GuiMenuManager? _guiManager;

        // ─────────────────────────────────────────────────────────
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Logger.Notification("[AxinMenuGUI] Iniciando lado servidor...");

            _serverChannel = api.Network
                .RegisterChannel("axinmenugui")
                .RegisterMessageType<MenuOpenPacket>()
                .RegisterMessageType<MenuClosePacket>()
                .RegisterMessageType<SlotClickPacket>()
                .RegisterMessageType<ExecuteCommandPacket>()
                .SetMessageHandler<SlotClickPacket>(OnSlotClickPacket);

            _playerData = new PlayerDataStore(api);
            _registry   = new MenuRegistry(api);
            _engine     = new MenuEngine(api, _registry, _playerData, _serverChannel);
            _commands   = new CommandHandler(api, _registry, _engine);

            _registry.LoadAll();

            api.Logger.Notification(
                $"[AxinMenuGUI] Listo. {_registry.Count} menú(s) cargado(s).");
        }

        // ─────────────────────────────────────────────────────────
        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("[AxinMenuGUI] Iniciando lado cliente...");
            _guiManager = new GuiMenuManager(api);
            api.Logger.Notification("[AxinMenuGUI] Cliente listo.");
        }

        // ─────────────────────────────────────────────────────────
        private void OnSlotClickPacket(IServerPlayer player, SlotClickPacket packet)
        {
            _engine?.HandleSlotClick(player, packet.MenuId, packet.Scene, packet.SlotIndex);
        }

        // ─────────────────────────────────────────────────────────
        public override void Dispose()
        {
            _registry?.Dispose();
            _playerData?.Dispose();
            base.Dispose();
        }
    }
}
