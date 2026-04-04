// AxinMenuGUI — UI
// Archivo: GuiMenuDialog.cs
// Responsabilidad: renderizado del GUI en pantalla (ClientSide).
//   Gestiona la composición del diálogo, slots, tooltips y navegación de escenas.
//
// SISTEMA DE TEMAS: la lógica de colores y el dibujo Cairo están en GuiThemeRenderer.cs.
//   Temas disponibles: default | dark-red | dark-blue | dark-green | parchment | stone | night
//   y variantes cofre (-2), más glass.
//   Si theme está vacío o ausente → "default" (aspecto estándar de VS).

using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AxinMenuGUI
{
    public class GuiMenuDialog : GuiDialog
    {
        private const int Cols = 9;

        private readonly NetMenu     _menu;
        private int                  _currentScene;
        private readonly Action<int> _onSlotClick;
        private MenuTheme            _theme;
        private bool                 _useCustomDraw;

        private Dictionary<int, (string name, string lore)> _tooltipBySlot = new();
        private List<int>  _indexToSlot = new();
        private double     _slotS, _pad;
        private int        _gridRows;
        private bool       _debugLogSlot      = true;
        private int        _firstOccupiedSlot  = -1;

        public override string ToggleKeyCombinationCode => null;
        public override bool   PrefersUngrabbedMouse    => false;

        public GuiMenuDialog(ICoreClientAPI capi, NetMenu menu, int startScene, Action<int> onSlotClick)
            : base(capi)
        {
            _menu          = menu;
            _currentScene  = startScene;
            _onSlotClick   = onSlotClick;
            // _theme y _useCustomDraw se calculan en SetupDialog (pueden cambiar por escena)
            SetupDialog();
        }

        private void SetupDialog()
        {
            var scene = _menu.Scenes.Find(s => s.Key == _currentScene.ToString());

            // Tema efectivo: la escena puede sobreescribir el tema del menú
            string effectiveTheme = (scene != null && !string.IsNullOrWhiteSpace(scene.Theme))
                ? scene.Theme
                : _menu.Theme;
            _theme         = MenuThemes.Get(effectiveTheme);
            _useCustomDraw = !string.IsNullOrWhiteSpace(effectiveTheme)
                             && effectiveTheme.ToLowerInvariant() != "default";

            _pad   = GuiElementItemSlotGridBase.unscaledSlotPadding;
            _slotS = GuiElementPassiveItemSlot.unscaledSlotSize;

            int totalSlots = _menu.Rows * Cols;
            _gridRows      = _menu.Rows;

            var skillItems = new SkillItem[totalSlots];
            _indexToSlot   = new List<int>();
            _tooltipBySlot = new Dictionary<int, (string, string)>();

            var emptyBlock = capi.World.GetBlock(new AssetLocation("game:air"));
            var emptyStack = new ItemStack(emptyBlock);

            for (int i = 0; i < totalSlots; i++)
            {
                skillItems[i] = new SkillItem
                {
                    Code          = new AssetLocation("game:air"),
                    Name          = "",
                    Data          = emptyStack,
                    RenderHandler = (loc, dt, px, py) => { }
                };
                _indexToSlot.Add(-1);
            }

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
                    if (col is Item vsItem) stack = new ItemStack(vsItem,    Math.Max(1, item.Amount));
                    else                    stack = new ItemStack((Block)col, Math.Max(1, item.Amount));
                    stack.ResolveBlockOrItem(capi.World);

                    string name = !string.IsNullOrWhiteSpace(item.Name)
                        ? item.Name : col.GetHeldItemName(stack);

                    var captured = stack;
                    double ss    = _slotS;

                    skillItems[slotIndex] = new SkillItem
                    {
                        Code          = col.Code,
                        Name          = name,
                        Description   = item.Lore,
                        Data          = captured,
                        RenderHandler = (assetLoc, dt, posX, posY) =>
                        {
                            if (_debugLogSlot && slotIndex == _firstOccupiedSlot)
                            {
                                capi.Logger.Notification(
                                    $"[AMG-CAL] slot={slotIndex} vc={slotIndex%Cols} vr={slotIndex/Cols} " +
                                    $"posX={posX:F2} posY={posY:F2} ss={ss:F2} " +
                                    $"pad={_pad:F2} slotS={_slotS:F2} " +
                                    $"cairoGx={(_theme.BorderW+_theme.InnerPad):F2} " +
                                    $"cairoGy={(_theme.TitleH+_theme.BorderW+_theme.InnerPad):F2}");
                                _debugLogSlot = false;
                            }
                            capi.Render.RenderItemstackToGui(
                                new DummySlot(captured),
                                posX + ss * 0.5,
                                posY + ss * 0.5,
                                100,
                                (float)(ss * 0.60),
                                ColorUtil.WhiteArgb);
                        }
                    };
                    _indexToSlot[slotIndex]   = slotIndex;
                    _tooltipBySlot[slotIndex] = (name, item.Lore);
                    if (_firstOccupiedSlot < 0) _firstOccupiedSlot = slotIndex;
                }
            }

            double gridW = Cols      * (_slotS + _pad);
            double gridH = _gridRows * (_slotS + _pad);

            GuiComposer composer;

            if (_useCustomDraw)
            {
                // ── Tema personalizado ──────────────────────────────────
                double bw = _theme.BorderW;
                double ip = _theme.InnerPad;
                double th = _theme.TitleH;

                double totalW = gridW + (ip + bw) * 2;
                double totalH = gridH + th + (ip + bw) * 2;

                ElementBounds dialogBounds = ElementStdBounds
                    .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                    .WithFixedSize(totalW, totalH);

                ElementBounds bgBounds   = ElementBounds.Fixed(0, 0, totalW, totalH);
                ElementBounds gridBounds = ElementBounds.Fixed(bw + ip, th + bw + ip, gridW, gridH);

                // Captura de variables para el lambda del DrawBackground
                var capturedTheme   = _theme;
                var capturedTitle   = _menu.Title;
                int capturedCols    = Cols;
                int capturedRows    = _gridRows;
                double capturedSlotS = _slotS;
                double capturedPad   = _pad;

                composer = capi.Gui
                    .CreateCompo("axinmenugui-" + _menu.Id + "-s" + _currentScene, dialogBounds)
                    .AddStaticCustomDraw(bgBounds, (ctx, surface, bounds) =>
                        GuiThemeRenderer.DrawBackground(
                            ctx, surface, bounds,
                            capturedTheme, capturedTitle,
                            capturedCols, capturedRows,
                            capturedSlotS, capturedPad))
                    .AddSkillItemGrid(skillItems.ToList(), Cols, _gridRows, OnSkillItemClick, gridBounds, "skillgrid");

                foreach (var (slotIdx, (name, lore)) in _tooltipBySlot)
                {
                    string text = string.IsNullOrWhiteSpace(lore) ? name : name + "\n" + lore;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    int vc = slotIdx % Cols;
                    int vr = slotIdx / Cols;
                    ElementBounds sb = gridBounds.FlatCopy()
                        .WithFixedOffset(vc * (_slotS + _pad), vr * (_slotS + _pad))
                        .WithFixedSize(_slotS, _slotS);
                    composer.AddHoverText(text, CairoFont.WhiteSmallText(), 250, sb,
                        "hover-" + _menu.Id + "-s" + _currentScene + "-" + slotIdx);
                }
            }
            else
            {
                // ── Tema default — aspecto estándar de VS ───────────────
                int titleBarH = (int)GuiStyle.TitleBarHeight;
                int dialogPad = (int)GuiStyle.ElementToDialogPadding;

                ElementBounds dialogBounds = ElementStdBounds
                    .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

                ElementBounds gridBounds = ElementBounds.Fixed(
                    dialogPad, titleBarH + dialogPad, gridW, gridH);

                ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(dialogPad);
                bgBounds.BothSizing = ElementSizing.FitToChildren;
                bgBounds.WithChildren(gridBounds);

                composer = capi.Gui
                    .CreateCompo("axinmenugui-" + _menu.Id + "-s" + _currentScene, dialogBounds)
                    .AddShadedDialogBG(bgBounds)
                    .AddDialogTitleBar(_menu.Title, () => TryClose())
                    .AddSkillItemGrid(skillItems.ToList(), Cols, _gridRows, OnSkillItemClick, gridBounds, "skillgrid");

                foreach (var (slotIdx, (name, lore)) in _tooltipBySlot)
                {
                    string text = string.IsNullOrWhiteSpace(lore) ? name : name + "\n" + lore;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    int vc = slotIdx % Cols;
                    int vr = slotIdx / Cols;
                    ElementBounds sb = gridBounds.FlatCopy()
                        .WithFixedOffset(vc * (_slotS + _pad), vr * (_slotS + _pad))
                        .WithFixedSize(_slotS, _slotS);
                    composer.AddHoverText(text, CairoFont.WhiteSmallText(), 250, sb,
                        "hover-" + _menu.Id + "-s" + _currentScene + "-" + slotIdx);
                }
            }

            SingleComposer = composer.Compose();
        }

        private void OnSkillItemClick(int visualIndex)
        {
            if (visualIndex < 0 || visualIndex >= _indexToSlot.Count) return;
            int slotJson = _indexToSlot[visualIndex];
            if (slotJson < 0) return;
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
