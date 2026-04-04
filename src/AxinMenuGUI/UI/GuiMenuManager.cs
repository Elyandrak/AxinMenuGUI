// AxinMenuGUI — UI
// Archivo: GuiMenuManager.cs
// CAMBIO v0.6.5: añadido RegisterMessageType<BlockRightClickPacket> al canal cliente.
//   BlockClickHandlerClient se inicializa aquí con acceso al channel.

using Vintagestory.API.Client;

namespace AxinMenuGUI
{
    public class GuiMenuManager
    {
        private readonly ICoreClientAPI    _api;
        private GuiMenuDialog?             _activeDialog;
        private IClientNetworkChannel?     _channel;

        // Accesible para que AxinMenuGuiMod pueda inyectar BlockClickHandlerClient
        public IClientNetworkChannel? Channel => _channel;

        public GuiMenuManager(ICoreClientAPI api)
        {
            _api = api;

            _channel = api.Network
                .RegisterChannel("axinmenugui")
                .RegisterMessageType<MenuOpenPacket>()
                .RegisterMessageType<MenuClosePacket>()
                .RegisterMessageType<SlotClickPacket>()
                .RegisterMessageType<ExecuteCommandPacket>()
                .RegisterMessageType<BlockRightClickPacket>()
                .RegisterMessageType<BlockGuiListPacket>()
                .SetMessageHandler<MenuOpenPacket>(OnMenuOpenPacket)
                .SetMessageHandler<MenuClosePacket>(OnMenuClosePacket)
                .SetMessageHandler<ExecuteCommandPacket>(OnExecuteCommandPacket);

            api.Logger.Notification("[AxinMenuGUI] Canal de red cliente registrado.");
        }

        private void OnMenuOpenPacket(MenuOpenPacket packet)
        {
            if (packet?.Menu == null) return;

            _api.Logger.Notification(
                $"[AxinMenuGUI] Paquete recibido: abriendo '{packet.Menu.Id}' escena {packet.Scene}");

            _activeDialog?.TryClose();
            _activeDialog = new GuiMenuDialog(
                _api,
                packet.Menu,
                packet.Scene,
                slotIndex => _channel?.SendPacket(new SlotClickPacket
                {
                    MenuId    = packet.Menu.Id,
                    Scene     = packet.Scene,
                    SlotIndex = slotIndex
                }));

            _activeDialog.TryOpen();
        }

        private void OnMenuClosePacket(MenuClosePacket packet)
        {
            _activeDialog?.TryClose();
            _activeDialog = null;
        }

        private void OnExecuteCommandPacket(ExecuteCommandPacket packet)
        {
            if (string.IsNullOrWhiteSpace(packet.Command)) return;
            _api.SendChatMessage(packet.Command);
        }
    }
}
