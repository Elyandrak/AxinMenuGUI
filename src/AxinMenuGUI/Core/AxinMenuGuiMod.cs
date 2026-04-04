// AxinMenuGUI — Core
// Archivo: AxinMenuGuiMod.cs
// CAMBIO v0.6.5: BlockClick usa arquitectura packet cliente→servidor sin Harmony.
//   BlockClickHandlerServer gestiona pending y registry en el servidor.
//   BlockClickHandlerClient detecta clic derecho en el cliente y envía packet.

using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace AxinMenuGUI
{
    public class AxinMenuGuiMod : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        // ── Server-side ──────────────────────────────────────────
        private ConfigManager?            _configManager;
        private MenuRegistry?             _registry;
        private MenuEngine?               _engine;
        private PlayerDataStore?          _playerData;
        private PlayerStatsTracker?       _statsTracker;
        private RankingService?           _ranking;
        private CommandHandler?           _commands;
        private BlockClickRegistry?       _blockClickRegistry;
        private BlockClickHandlerServer?  _blockClickServer;
        private IServerNetworkChannel?    _serverChannel;

        // ── Client-side ──────────────────────────────────────────
        private GuiMenuManager?           _guiManager;
        private BlockClickHandlerClient?  _blockClickClient;

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Logger.Notification("[AxinMenuGUI] Iniciando lado servidor...");

            _serverChannel = api.Network
                .RegisterChannel("axinmenugui")
                .RegisterMessageType<MenuOpenPacket>()
                .RegisterMessageType<MenuClosePacket>()
                .RegisterMessageType<SlotClickPacket>()
                .RegisterMessageType<ExecuteCommandPacket>()
                .RegisterMessageType<BlockRightClickPacket>()
                .RegisterMessageType<BlockGuiListPacket>()
                .SetMessageHandler<SlotClickPacket>(OnSlotClickPacket)
                .SetMessageHandler<BlockRightClickPacket>(OnBlockRightClickPacket);

            _configManager     = new ConfigManager(api);
            _playerData        = new PlayerDataStore(api);
            _statsTracker      = new PlayerStatsTracker(api, _playerData, _configManager);
            _ranking           = new RankingService(api, _configManager);
            _registry          = new MenuRegistry(api);
            _engine            = new MenuEngine(api, _registry, _playerData, _serverChannel, _ranking);
            _commands          = new CommandHandler(api, _registry, _engine, _playerData, _ranking);

            _blockClickRegistry = new BlockClickRegistry(api);
            _blockClickServer   = new BlockClickHandlerServer(
                api, _blockClickRegistry, _engine, _registry, _serverChannel!);
            _commands.SetBlockClickHandler(_blockClickServer);

            _registry.LoadAll();

            api.Logger.Notification(
                $"[AxinMenuGUI] Listo. {_registry.Count} menú(s) cargado(s).");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("[AxinMenuGUI] Iniciando lado cliente...");

            _guiManager      = new GuiMenuManager(api);
            _blockClickClient = new BlockClickHandlerClient(api);

            // Inyectar el channel ya registrado por GuiMenuManager
            if (_guiManager.Channel != null)
                _blockClickClient.Init(_guiManager.Channel);

            api.Logger.Notification("[AxinMenuGUI] Cliente listo.");
        }

        private void OnSlotClickPacket(IServerPlayer player, SlotClickPacket packet)
        {
            _engine?.HandleSlotClick(player, packet.MenuId, packet.Scene, packet.SlotIndex);
        }

        private void OnBlockRightClickPacket(IServerPlayer player, BlockRightClickPacket packet)
        {
            _blockClickServer?.OnBlockRightClick(player, packet);
        }

        public override void Dispose()
        {
            _statsTracker?.SaveAllOnline();
            _statsTracker?.Dispose();
            _registry?.Dispose();
            _playerData?.Dispose();
            base.Dispose();
        }
    }
}
