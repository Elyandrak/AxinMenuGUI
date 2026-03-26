// AxinMenuGUI — UI
// Archivo: GuiMenuDialog.cs
// Responsabilidad: renderizado del GUI en pantalla (ClientSide).
//
// NORMA: AddSkillItemGrid coloca ítems secuencialmente ignorando el slot JSON.
// SOLUCIÓN: rellenar el grid con N=rows*Cols slots, colocando ítems en su slot
// exacto y dejando slots vacíos (transparentes) en los demás.
// Así slot JSON = posición visual real, y AddHoverText queda alineado.

using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AxinMenuGUI
{
    public class GuiMenuDialog : GuiDialog
    {
        private static readonly int TitleBarH     = (int)GuiStyle.TitleBarHeight;
        private static readonly int DialogPadding = (int)GuiStyle.ElementToDialogPadding;
        private const int Cols = 9;

        private readonly NetMenu     _menu;
        private int                  _currentScene;
        private readonly Action<int> _onSlotClick;

        // slot JSON → (nombre, lore) para ítems con contenido
        private Dictionary<int, (string name, string lore)> _tooltipBySlot = new();
        // slot JSON → índice en _indexToSlot (para el click handler)
        private List<int> _indexToSlot = new();
        private double _slotS, _pad;
        private int    _gridRows;

        public override string ToggleKeyCombinationCode => null;
        public override bool PrefersUngrabbedMouse => false;

        public GuiMenuDialog(ICoreClientAPI capi, NetMenu menu, int startScene, Action<int> onSlotClick)
            : base(capi)
        {
            _menu         = menu;
            _currentScene = startScene;
            _onSlotClick  = onSlotClick;
            SetupDialog();
        }

        private void SetupDialog()
        {
            var scene = _menu.Scenes.Find(s => s.Key == _currentScene.ToString());

            _pad   = GuiElementItemSlotGridBase.unscaledSlotPadding;
            _slotS = GuiElementPassiveItemSlot.unscaledSlotSize;

            int totalSlots = _menu.Rows * Cols;
            _gridRows      = _menu.Rows;

            // Grid completo: un SkillItem por cada slot del grid
            var skillItems   = new SkillItem[totalSlots];
            _indexToSlot     = new List<int>();
            _tooltipBySlot   = new Dictionary<int, (string, string)>();

            // Slot vacío reutilizable (transparente, sin RenderHandler)
            var emptyBlock = capi.World.GetBlock(new AssetLocation("game:air"));
            var emptyStack = new ItemStack(emptyBlock);

            // Rellenar todo el grid con slots vacíos por defecto
            for (int i = 0; i < totalSlots; i++)
            {
                skillItems[i] = new SkillItem
                {
                    Code = new AssetLocation("game:air"),
                    Name = "",
                    Data = emptyStack,
                    RenderHandler = (loc, dt, px, py) => { } // render vacío
                };
                _indexToSlot.Add(-1); // -1 = slot vacío, no clicable
            }

            // Colocar ítems reales en su slot exacto
            if (scene != null)
            {
                foreach (var item in scene.Items)
                {
                    int slotIndex = item.Slot;
                    if (slotIndex < 0 || slotIndex >= totalSlots) continue;

                    var loc = new AssetLocation(item.ItemCode);

                    CollectibleObject? col = capi.World.GetItem(loc);
                    if (col == null) col = capi.World.GetBlock(loc);

                    if (col == null)
                        foreach (var vi in capi.World.Items)
                            if (vi?.Code?.Path != null && vi.Code.Path.StartsWith(loc.Path))
                            { col = vi; break; }

                    if (col == null)
                        foreach (var vb in capi.World.Blocks)
                            if (vb?.Code?.Path != null && vb.Code.Path.StartsWith(loc.Path))
                            { col = vb; break; }

                    if (col == null)
                    {
                        capi.Logger.Warning($"[AxinMenuGUI] Ítem no encontrado: '{item.ItemCode}' (slot {slotIndex})");
                        continue;
                    }

                    ItemStack stack;
                    if (col is Item vsItem) stack = new ItemStack(vsItem,   Math.Max(1, item.Amount));
                    else                    stack = new ItemStack((Block)col, Math.Max(1, item.Amount));
                    stack.ResolveBlockOrItem(capi.World);

                    string name = !string.IsNullOrWhiteSpace(item.Name)
                        ? item.Name
                        : col.GetHeldItemName(stack);

                    var captured = stack;
                    skillItems[slotIndex] = new SkillItem
                    {
                        Code          = col.Code,
                        Name          = name,
                        Description   = item.Lore,
                        Data          = captured,
                        RenderHandler = (assetLoc, dt, posX, posY) =>
                        {
                            double size = GuiElementPassiveItemSlot.unscaledSlotSize;
                            capi.Render.RenderItemstackToGui(
                                new DummySlot(captured),
                                posX + size * 0.5,
                                posY + size * 0.5,
                                100,
                                (float)(size * 0.58),
                                ColorUtil.WhiteArgb
                            );
                        }
                    };
                    _indexToSlot[slotIndex] = slotIndex; // slot JSON = índice visual
                    _tooltipBySlot[slotIndex] = (name, item.Lore);
                }
            }

            double gridW = Cols      * (_slotS + _pad);
            double gridH = _gridRows * (_slotS + _pad);

            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            ElementBounds gridBounds = ElementBounds.Fixed(
                DialogPadding, TitleBarH + DialogPadding, gridW, gridH);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(DialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(gridBounds);

            var composer = capi.Gui
                .CreateCompo("axinmenugui-" + _menu.Id + "-s" + _currentScene, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(_menu.Title, () => TryClose())
                .AddSkillItemGrid(skillItems.ToList(), Cols, _gridRows, OnSkillItemClick, gridBounds, "skillgrid");

            // HoverText: bounds relativos al gridBounds para alineación exacta
            foreach (var (slotIdx, (name, lore)) in _tooltipBySlot)
            {
                string text = string.IsNullOrWhiteSpace(lore) ? name : name + "\n" + lore;
                if (string.IsNullOrWhiteSpace(text)) continue;

                int vc = slotIdx % Cols;
                int vr = slotIdx / Cols;

                // Usamos gridBounds como padre — mismo origen que AddSkillItemGrid
                ElementBounds slotBounds = gridBounds.FlatCopy().WithFixedOffset(
                    vc * (_slotS + _pad),
                    vr * (_slotS + _pad))
                    .WithFixedSize(_slotS, _slotS);

                composer.AddHoverText(text, CairoFont.WhiteSmallText(), 250, slotBounds,
                    "hover-" + _menu.Id + "-s" + _currentScene + "-" + slotIdx);
            }

            SingleComposer = composer.Compose();
        }

        private void OnSkillItemClick(int visualIndex)
        {
            // visualIndex = slot JSON (porque el grid es 1:1 con los slots)
            if (visualIndex < 0 || visualIndex >= _indexToSlot.Count) return;
            int slotJson = _indexToSlot[visualIndex];
            if (slotJson < 0) return; // slot vacío
            _onSlotClick?.Invoke(slotJson);
        }

        public void NavigateToScene(int sceneIndex)
        {
            _currentScene = sceneIndex;
            SingleComposer?.Dispose();
            SetupDialog();
        }
    }
}
