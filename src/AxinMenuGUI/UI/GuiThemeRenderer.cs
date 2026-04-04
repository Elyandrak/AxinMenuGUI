// AxinMenuGUI — UI
// Archivo: GuiThemeRenderer.cs
// Responsabilidad: definición de temas visuales Cairo y renderizado del fondo de menú.
//   - MenuTheme: struct con todos los parámetros visuales de un tema.
//   - MenuThemes: catálogo estático de los 14 temas disponibles.
//   - GuiThemeRenderer: métodos de dibujo Cairo (DrawBackground, RoundedRect).
// NO gestiona el diálogo, los slots ni la navegación. Solo pinta.

using System;
using Cairo;
using Vintagestory.API.Client;

namespace AxinMenuGUI
{
    // ══════════════════════════════════════════════════════════════════════════
    // STRUCT DE TEMA
    // ══════════════════════════════════════════════════════════════════════════

    public struct MenuTheme
    {
        // Panel
        public double[] BgTop, BgBottom;
        // Borde
        public double[] BorderOuter, BorderInner, BorderGlow;
        // Título
        public double[] TitleTop, TitleBottom, TitleText, TitleShadow;
        // Separador
        public double[] DivBright, DivDark;
        // Slots
        public double[] SlotBg, SlotBorder;
        // Geometría
        public double CornerR, BorderW, TitleH, InnerPad;
        // Fuente título
        public FontSlant  TitleSlant;
        public FontWeight TitleWeight;
        public double     TitleSize;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CATÁLOGO DE TEMAS (14 temas)
    // ══════════════════════════════════════════════════════════════════════════

    public static class MenuThemes
    {
        private static double[] C(double r, double g, double b, double a = 1.0)
            => new[] { r, g, b, a };

        public static MenuTheme Get(string name) => (name ?? "default").ToLowerInvariant() switch
        {
            "dark-red"     => DarkRed,
            "dark-blue"    => DarkBlue,
            "dark-green"   => DarkGreen,
            "parchment"    => Parchment,
            "stone"        => Stone,
            "night"        => Night,
            "dark-red-2"   => DarkRed2,
            "dark-blue-2"  => DarkBlue2,
            "dark-green-2" => DarkGreen2,
            "parchment-2"  => Parchment2,
            "stone-2"      => Stone2,
            "night-2"      => Night2,
            "glass"        => Glass,
            _              => Default,
        };

        // ── DEFAULT — aspecto cálido estándar VS (sin custom draw) ───────────
        public static readonly MenuTheme Default = new()
        {
            CornerR = 0, BorderW = 0, TitleH = 0, InnerPad = 0,
            BgTop    = C(0,0,0,0), BgBottom = C(0,0,0,0),
            BorderOuter = C(0,0,0,0), BorderInner = C(0,0,0,0), BorderGlow = C(0,0,0,0),
            TitleTop = C(0,0,0,0), TitleBottom = C(0,0,0,0),
            TitleText = C(1,1,1,1), TitleShadow = C(0,0,0,0),
            DivBright = C(0,0,0,0), DivDark = C(0,0,0,0),
            SlotBg = C(0,0,0,0), SlotBorder = C(0,0,0,0),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 13,
        };

        // ── DARK-RED — panel oscuro, borde rojo, dorado ──────────────────────
        public static readonly MenuTheme DarkRed = new()
        {
            CornerR = 8, BorderW = 4, TitleH = 30, InnerPad = 5,
            BgTop    = C(0.14, 0.09, 0.07, 0.97),
            BgBottom = C(0.06, 0.03, 0.02, 0.97),
            BorderOuter = C(0.60, 0.08, 0.06, 1.0),
            BorderInner = C(0.92, 0.28, 0.10, 1.0),
            BorderGlow  = C(0.70, 0.10, 0.05, 0.25),
            TitleTop    = C(0.52, 0.07, 0.05, 1.0),
            TitleBottom = C(0.28, 0.03, 0.02, 1.0),
            TitleText   = C(1.00, 0.92, 0.55, 1.0),
            TitleShadow = C(0.18, 0.04, 0.02, 0.9),
            DivBright   = C(0.95, 0.35, 0.10, 0.9),
            DivDark     = C(0.38, 0.05, 0.03, 0.8),
            SlotBg      = C(0.20, 0.11, 0.07, 0.80),
            SlotBorder  = C(0.70, 0.18, 0.08, 0.65),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 12,
        };

        // ── DARK-BLUE — panel oscuro, borde zafiro, plateado ─────────────────
        public static readonly MenuTheme DarkBlue = new()
        {
            CornerR = 8, BorderW = 4, TitleH = 30, InnerPad = 5,
            BgTop    = C(0.07, 0.10, 0.18, 0.97),
            BgBottom = C(0.03, 0.05, 0.10, 0.97),
            BorderOuter = C(0.10, 0.25, 0.65, 1.0),
            BorderInner = C(0.30, 0.60, 1.00, 1.0),
            BorderGlow  = C(0.15, 0.35, 0.90, 0.25),
            TitleTop    = C(0.10, 0.20, 0.50, 1.0),
            TitleBottom = C(0.05, 0.10, 0.28, 1.0),
            TitleText   = C(0.80, 0.92, 1.00, 1.0),
            TitleShadow = C(0.02, 0.05, 0.15, 0.9),
            DivBright   = C(0.40, 0.70, 1.00, 0.9),
            DivDark     = C(0.08, 0.18, 0.45, 0.8),
            SlotBg      = C(0.08, 0.13, 0.28, 0.80),
            SlotBorder  = C(0.20, 0.50, 0.90, 0.60),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 12,
        };

        // ── DARK-GREEN — panel oscuro, borde esmeralda, dorado claro ─────────
        public static readonly MenuTheme DarkGreen = new()
        {
            CornerR = 8, BorderW = 4, TitleH = 30, InnerPad = 5,
            BgTop    = C(0.07, 0.14, 0.08, 0.97),
            BgBottom = C(0.03, 0.07, 0.04, 0.97),
            BorderOuter = C(0.08, 0.45, 0.12, 1.0),
            BorderInner = C(0.20, 0.85, 0.30, 1.0),
            BorderGlow  = C(0.10, 0.60, 0.15, 0.25),
            TitleTop    = C(0.08, 0.38, 0.12, 1.0),
            TitleBottom = C(0.04, 0.18, 0.06, 1.0),
            TitleText   = C(0.85, 1.00, 0.65, 1.0),
            TitleShadow = C(0.02, 0.10, 0.03, 0.9),
            DivBright   = C(0.25, 0.90, 0.35, 0.9),
            DivDark     = C(0.06, 0.30, 0.10, 0.8),
            SlotBg      = C(0.07, 0.18, 0.08, 0.80),
            SlotBorder  = C(0.15, 0.65, 0.22, 0.60),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 12,
        };

        // ── PARCHMENT — pergamino cálido, borde madera, texto oscuro ─────────
        public static readonly MenuTheme Parchment = new()
        {
            CornerR = 5, BorderW = 5, TitleH = 30, InnerPad = 6,
            BgTop    = C(0.85, 0.75, 0.52, 0.97),
            BgBottom = C(0.72, 0.60, 0.38, 0.97),
            BorderOuter = C(0.42, 0.26, 0.10, 1.0),
            BorderInner = C(0.65, 0.45, 0.20, 1.0),
            BorderGlow  = C(0.50, 0.32, 0.12, 0.20),
            TitleTop    = C(0.55, 0.35, 0.14, 1.0),
            TitleBottom = C(0.38, 0.22, 0.08, 1.0),
            TitleText   = C(0.98, 0.92, 0.72, 1.0),
            TitleShadow = C(0.20, 0.10, 0.03, 0.8),
            DivBright   = C(0.70, 0.52, 0.25, 0.9),
            DivDark     = C(0.30, 0.18, 0.06, 0.8),
            SlotBg      = C(0.62, 0.50, 0.30, 0.70),
            SlotBorder  = C(0.40, 0.25, 0.10, 0.70),
            TitleSlant = FontSlant.Italic, TitleWeight = FontWeight.Bold, TitleSize = 12,
        };

        // ── STONE — piedra gris, borde plata, blanco frío ────────────────────
        public static readonly MenuTheme Stone = new()
        {
            CornerR = 4, BorderW = 4, TitleH = 28, InnerPad = 5,
            BgTop    = C(0.28, 0.28, 0.30, 0.97),
            BgBottom = C(0.14, 0.14, 0.16, 0.97),
            BorderOuter = C(0.50, 0.50, 0.55, 1.0),
            BorderInner = C(0.80, 0.82, 0.88, 1.0),
            BorderGlow  = C(0.60, 0.60, 0.65, 0.20),
            TitleTop    = C(0.40, 0.40, 0.44, 1.0),
            TitleBottom = C(0.22, 0.22, 0.25, 1.0),
            TitleText   = C(0.92, 0.95, 1.00, 1.0),
            TitleShadow = C(0.06, 0.06, 0.08, 0.9),
            DivBright   = C(0.78, 0.80, 0.88, 0.9),
            DivDark     = C(0.18, 0.18, 0.22, 0.8),
            SlotBg      = C(0.20, 0.20, 0.23, 0.80),
            SlotBorder  = C(0.55, 0.58, 0.65, 0.60),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 12,
        };

        // ── NIGHT — negro, borde violeta, cyan mágico ────────────────────────
        public static readonly MenuTheme Night = new()
        {
            CornerR = 10, BorderW = 4, TitleH = 30, InnerPad = 5,
            BgTop    = C(0.06, 0.04, 0.12, 0.98),
            BgBottom = C(0.02, 0.01, 0.06, 0.98),
            BorderOuter = C(0.40, 0.10, 0.70, 1.0),
            BorderInner = C(0.75, 0.35, 1.00, 1.0),
            BorderGlow  = C(0.55, 0.15, 0.90, 0.30),
            TitleTop    = C(0.25, 0.08, 0.45, 1.0),
            TitleBottom = C(0.10, 0.03, 0.20, 1.0),
            TitleText   = C(0.55, 1.00, 0.95, 1.0),
            TitleShadow = C(0.15, 0.04, 0.30, 0.9),
            DivBright   = C(0.65, 0.30, 1.00, 0.9),
            DivDark     = C(0.20, 0.05, 0.40, 0.8),
            SlotBg      = C(0.08, 0.04, 0.16, 0.80),
            SlotBorder  = C(0.50, 0.20, 0.85, 0.60),
            TitleSlant = FontSlant.Italic, TitleWeight = FontWeight.Bold, TitleSize = 12,
        };

        // ── DARK-RED-2 — estilo cofre: rojo sangre, marco grueso 7px ─────────
        public static readonly MenuTheme DarkRed2 = new()
        {
            CornerR = 2, BorderW = 7, TitleH = 26, InnerPad = 3,
            BgTop    = C(0.32, 0.06, 0.04, 0.98),
            BgBottom = C(0.18, 0.03, 0.02, 0.98),
            BorderOuter = C(0.85, 0.18, 0.10, 1.0),
            BorderInner = C(0.20, 0.03, 0.01, 1.0),
            BorderGlow  = C(0.70, 0.08, 0.04, 0.15),
            TitleTop    = C(0.65, 0.10, 0.06, 1.0),
            TitleBottom = C(0.35, 0.05, 0.02, 1.0),
            TitleText   = C(1.00, 0.95, 0.60, 1.0),
            TitleShadow = C(0.10, 0.02, 0.01, 0.9),
            DivBright   = C(0.95, 0.30, 0.10, 0.9),
            DivDark     = C(0.18, 0.02, 0.01, 0.8),
            SlotBg      = C(0.25, 0.05, 0.03, 0.70),
            SlotBorder  = C(0.80, 0.15, 0.08, 0.55),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 11,
        };

        // ── DARK-BLUE-2 — estilo cofre: azul océano, marco grueso 7px ────────
        public static readonly MenuTheme DarkBlue2 = new()
        {
            CornerR = 2, BorderW = 7, TitleH = 26, InnerPad = 3,
            BgTop    = C(0.05, 0.12, 0.35, 0.98),
            BgBottom = C(0.02, 0.06, 0.18, 0.98),
            BorderOuter = C(0.30, 0.60, 1.00, 1.0),
            BorderInner = C(0.03, 0.10, 0.30, 1.0),
            BorderGlow  = C(0.12, 0.30, 0.85, 0.15),
            TitleTop    = C(0.08, 0.22, 0.62, 1.0),
            TitleBottom = C(0.03, 0.10, 0.32, 1.0),
            TitleText   = C(0.75, 0.92, 1.00, 1.0),
            TitleShadow = C(0.01, 0.04, 0.18, 0.9),
            DivBright   = C(0.40, 0.72, 1.00, 0.9),
            DivDark     = C(0.04, 0.10, 0.30, 0.8),
            SlotBg      = C(0.06, 0.12, 0.32, 0.70),
            SlotBorder  = C(0.25, 0.55, 0.95, 0.55),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 11,
        };

        // ── DARK-GREEN-2 — estilo cofre: verde bosque, marco grueso 7px ──────
        public static readonly MenuTheme DarkGreen2 = new()
        {
            CornerR = 2, BorderW = 7, TitleH = 26, InnerPad = 3,
            BgTop    = C(0.05, 0.18, 0.06, 0.98),
            BgBottom = C(0.02, 0.09, 0.03, 0.98),
            BorderOuter = C(0.22, 0.75, 0.25, 1.0),
            BorderInner = C(0.03, 0.18, 0.05, 1.0),
            BorderGlow  = C(0.08, 0.55, 0.12, 0.15),
            TitleTop    = C(0.06, 0.48, 0.10, 1.0),
            TitleBottom = C(0.03, 0.22, 0.05, 1.0),
            TitleText   = C(0.80, 1.00, 0.60, 1.0),
            TitleShadow = C(0.01, 0.08, 0.02, 0.9),
            DivBright   = C(0.28, 0.90, 0.32, 0.9),
            DivDark     = C(0.03, 0.20, 0.06, 0.8),
            SlotBg      = C(0.05, 0.18, 0.06, 0.70),
            SlotBorder  = C(0.18, 0.70, 0.22, 0.55),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 11,
        };

        // ── PARCHMENT-2 — estilo cofre: madera oscura, marco 8px ─────────────
        public static readonly MenuTheme Parchment2 = new()
        {
            CornerR = 2, BorderW = 8, TitleH = 26, InnerPad = 3,
            BgTop    = C(0.45, 0.28, 0.10, 0.98),
            BgBottom = C(0.28, 0.16, 0.05, 0.98),
            BorderOuter = C(0.72, 0.50, 0.22, 1.0),
            BorderInner = C(0.20, 0.10, 0.03, 1.0),
            BorderGlow  = C(0.55, 0.35, 0.12, 0.15),
            TitleTop    = C(0.60, 0.40, 0.15, 1.0),
            TitleBottom = C(0.32, 0.18, 0.05, 1.0),
            TitleText   = C(1.00, 0.95, 0.75, 1.0),
            TitleShadow = C(0.12, 0.06, 0.01, 0.9),
            DivBright   = C(0.80, 0.60, 0.28, 0.9),
            DivDark     = C(0.18, 0.09, 0.02, 0.8),
            SlotBg      = C(0.35, 0.20, 0.07, 0.70),
            SlotBorder  = C(0.65, 0.45, 0.18, 0.60),
            TitleSlant = FontSlant.Italic, TitleWeight = FontWeight.Bold, TitleSize = 11,
        };

        // ── STONE-2 — estilo cofre: granito oscuro, marco 7px ────────────────
        public static readonly MenuTheme Stone2 = new()
        {
            CornerR = 2, BorderW = 7, TitleH = 26, InnerPad = 3,
            BgTop    = C(0.22, 0.22, 0.24, 0.98),
            BgBottom = C(0.10, 0.10, 0.12, 0.98),
            BorderOuter = C(0.80, 0.82, 0.88, 1.0),
            BorderInner = C(0.08, 0.08, 0.10, 1.0),
            BorderGlow  = C(0.50, 0.50, 0.58, 0.15),
            TitleTop    = C(0.38, 0.38, 0.42, 1.0),
            TitleBottom = C(0.18, 0.18, 0.22, 1.0),
            TitleText   = C(0.98, 1.00, 1.00, 1.0),
            TitleShadow = C(0.03, 0.03, 0.05, 0.9),
            DivBright   = C(0.88, 0.90, 0.96, 0.9),
            DivDark     = C(0.06, 0.06, 0.08, 0.8),
            SlotBg      = C(0.14, 0.14, 0.16, 0.70),
            SlotBorder  = C(0.72, 0.74, 0.80, 0.55),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Bold, TitleSize = 11,
        };

        // ── NIGHT-2 — estilo cofre: void negro, marco violeta 8px ────────────
        public static readonly MenuTheme Night2 = new()
        {
            CornerR = 2, BorderW = 8, TitleH = 26, InnerPad = 3,
            BgTop    = C(0.04, 0.02, 0.10, 0.99),
            BgBottom = C(0.01, 0.01, 0.05, 0.99),
            BorderOuter = C(0.75, 0.28, 1.00, 1.0),
            BorderInner = C(0.12, 0.03, 0.22, 1.0),
            BorderGlow  = C(0.60, 0.15, 0.95, 0.18),
            TitleTop    = C(0.35, 0.12, 0.60, 1.0),
            TitleBottom = C(0.15, 0.05, 0.28, 1.0),
            TitleText   = C(0.55, 1.00, 0.95, 1.0),
            TitleShadow = C(0.08, 0.02, 0.18, 0.9),
            DivBright   = C(0.80, 0.38, 1.00, 0.9),
            DivDark     = C(0.10, 0.02, 0.20, 0.8),
            SlotBg      = C(0.06, 0.02, 0.14, 0.70),
            SlotBorder  = C(0.60, 0.22, 0.90, 0.55),
            TitleSlant = FontSlant.Italic, TitleWeight = FontWeight.Bold, TitleSize = 11,
        };

        // ── GLASS — panel completamente transparente ──────────────────────────
        public static readonly MenuTheme Glass = new()
        {
            CornerR = 6, BorderW = 1, TitleH = 22, InnerPad = 4,
            BgTop    = C(0.0, 0.0, 0.0, 0.0),
            BgBottom = C(0.0, 0.0, 0.0, 0.0),
            BorderOuter = C(1.0, 1.0, 1.0, 0.18),
            BorderInner = C(1.0, 1.0, 1.0, 0.08),
            BorderGlow  = C(1.0, 1.0, 1.0, 0.05),
            TitleTop    = C(0.0, 0.0, 0.0, 0.0),
            TitleBottom = C(0.0, 0.0, 0.0, 0.0),
            TitleText   = C(1.0, 1.0, 1.0, 0.85),
            TitleShadow = C(0.0, 0.0, 0.0, 0.60),
            DivBright   = C(1.0, 1.0, 1.0, 0.12),
            DivDark     = C(1.0, 1.0, 1.0, 0.06),
            SlotBg      = C(1.0, 1.0, 1.0, 0.06),
            SlotBorder  = C(1.0, 1.0, 1.0, 0.15),
            TitleSlant = FontSlant.Normal, TitleWeight = FontWeight.Normal, TitleSize = 11,
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RENDERER CAIRO
    // ══════════════════════════════════════════════════════════════════════════

    public static class GuiThemeRenderer
    {
        /// <summary>
        /// Dibuja el fondo del menú sobre el canvas de AddStaticCustomDraw.
        /// Las coordenadas son unscaled; (0,0) = esquina superior izquierda del bounds.
        /// bounds.renderX/Y siempre valen 0 — NO hacer ctx.Translate.
        /// </summary>
        public static void DrawBackground(
            Context ctx,
            ImageSurface surface,
            ElementBounds bounds,
            MenuTheme t,
            string menuTitle,
            int cols,
            int gridRows,
            double slotS,
            double pad)
        {
            double w  = bounds.InnerWidth;
            double h  = bounds.InnerHeight;
            double bw = t.BorderW;
            double cr = t.CornerR;
            double th = t.TitleH;

            // 1. Halo exterior
            for (int i = 5; i >= 1; i--)
            {
                ctx.SetSourceRGBA(t.BorderGlow[0], t.BorderGlow[1], t.BorderGlow[2], t.BorderGlow[3] / i);
                RoundedRect(ctx, -i, -i, w + i * 2, h + i * 2, cr + i);
                ctx.Fill();
            }

            // 2. Fondo gradiente vertical
            using (var grad = new LinearGradient(0, 0, 0, h))
            {
                grad.AddColorStop(0, new Color(t.BgTop[0],    t.BgTop[1],    t.BgTop[2],    t.BgTop[3]));
                grad.AddColorStop(1, new Color(t.BgBottom[0], t.BgBottom[1], t.BgBottom[2], t.BgBottom[3]));
                ctx.SetSource(grad);
                RoundedRect(ctx, 0, 0, w, h, cr);
                ctx.Fill();
            }

            // 3. Borde exterior
            ctx.LineWidth = bw;
            ctx.SetSourceRGBA(t.BorderOuter[0], t.BorderOuter[1], t.BorderOuter[2], t.BorderOuter[3]);
            RoundedRect(ctx, bw / 2, bw / 2, w - bw, h - bw, cr);
            ctx.Stroke();

            // 4. Borde interior (efecto bisel)
            ctx.LineWidth = bw > 4 ? 2.5 : 1.0;
            ctx.SetSourceRGBA(t.BorderInner[0], t.BorderInner[1], t.BorderInner[2], t.BorderInner[3]);
            RoundedRect(ctx, bw + 1, bw + 1, w - bw * 2 - 2, h - bw * 2 - 2, Math.Max(1, cr - 2));
            ctx.Stroke();

            // 4b. Highlight del bisel (solo temas con borde grueso)
            if (bw > 4)
            {
                ctx.LineWidth = 1.0;
                ctx.SetSourceRGBA(t.BorderInner[0] * 1.8, t.BorderInner[1] * 1.8, t.BorderInner[2] * 1.8,
                    Math.Min(1.0, t.BorderInner[3] * 0.6));
                RoundedRect(ctx, bw * 0.35, bw * 0.35, w - bw * 0.7, h - bw * 0.7, cr - 1);
                ctx.Stroke();
            }

            // 5. Barra de título con gradiente
            double ti = bw + 1;
            double tw = w - ti * 2;
            using (var grad = new LinearGradient(0, ti, 0, th))
            {
                grad.AddColorStop(0, new Color(t.TitleTop[0],    t.TitleTop[1],    t.TitleTop[2],    t.TitleTop[3]));
                grad.AddColorStop(1, new Color(t.TitleBottom[0], t.TitleBottom[1], t.TitleBottom[2], t.TitleBottom[3]));
                ctx.SetSource(grad);
                ctx.MoveTo(ti + (cr - 2), ti);
                ctx.Arc(ti + tw - (cr - 2), ti + (cr - 2), cr - 2, -Math.PI / 2, 0);
                ctx.LineTo(ti + tw, th);
                ctx.LineTo(ti,      th);
                ctx.Arc(ti + (cr - 2), ti + (cr - 2), cr - 2, Math.PI, -Math.PI / 2);
                ctx.Fill();
            }

            // 6. Separador doble con ornamento
            ctx.LineWidth = 2.0;
            ctx.SetSourceRGBA(t.DivDark[0], t.DivDark[1], t.DivDark[2], t.DivDark[3]);
            ctx.MoveTo(bw + 2, th);     ctx.LineTo(w - bw - 2, th);     ctx.Stroke();
            ctx.LineWidth = 1.0;
            ctx.SetSourceRGBA(t.DivBright[0], t.DivBright[1], t.DivBright[2], t.DivBright[3]);
            ctx.MoveTo(bw + 2, th + 2); ctx.LineTo(w - bw - 2, th + 2); ctx.Stroke();
            double ornX = w / 2, ornY = th + 1;
            ctx.SetSourceRGBA(t.DivBright[0], t.DivBright[1], t.DivBright[2], 1.0);
            ctx.Arc(ornX, ornY, 4, 0, Math.PI * 2); ctx.Fill();
            ctx.SetSourceRGBA(t.BgBottom[0], t.BgBottom[1], t.BgBottom[2], 1.0);
            ctx.Arc(ornX, ornY, 2, 0, Math.PI * 2); ctx.Fill();

            // 7. Texto del título con sombra
            ctx.SelectFontFace("Sans", t.TitleSlant, t.TitleWeight);
            ctx.SetFontSize(t.TitleSize);
            var te = ctx.TextExtents(menuTitle);
            double tx = (w - te.Width) / 2.0;
            double ty = th * 0.70;
            ctx.SetSourceRGBA(t.TitleShadow[0], t.TitleShadow[1], t.TitleShadow[2], t.TitleShadow[3]);
            ctx.MoveTo(tx + 1, ty + 1); ctx.ShowText(menuTitle);
            ctx.SetSourceRGBA(t.TitleText[0], t.TitleText[1], t.TitleText[2], t.TitleText[3]);
            ctx.MoveTo(tx, ty);         ctx.ShowText(menuTitle);

            // 8. Área del grid — fondo unificado
            double gxU    = bw + t.InnerPad;
            double gyU    = th + bw + t.InnerPad;
            double gridWU = cols     * (slotS + pad);
            double gridHU = gridRows * (slotS + pad);

            ctx.SetSourceRGBA(t.SlotBg[0], t.SlotBg[1], t.SlotBg[2], t.SlotBg[3] * 0.4);
            RoundedRect(ctx, gxU, gyU, gridWU, gridHU, 3);
            ctx.Fill();

            double cell = slotS + pad;
            ctx.LineWidth = 0.5;
            ctx.SetSourceRGBA(t.SlotBorder[0], t.SlotBorder[1], t.SlotBorder[2], t.SlotBorder[3] * 0.4);
            for (int row = 1; row < gridRows; row++)
            {
                double ly = gyU + row * cell;
                ctx.MoveTo(gxU + 2, ly); ctx.LineTo(gxU + gridWU - 2, ly); ctx.Stroke();
            }
            for (int col = 1; col < cols; col++)
            {
                double lx = gxU + col * cell;
                ctx.MoveTo(lx, gyU + 2); ctx.LineTo(lx, gyU + gridHU - 2); ctx.Stroke();
            }
        }

        public static void RoundedRect(Context ctx, double x, double y, double w, double h, double r)
        {
            r = Math.Min(r, Math.Min(w, h) / 2);
            ctx.Arc(x + r,     y + r,     r, Math.PI,       Math.PI * 1.5);
            ctx.Arc(x + w - r, y + r,     r, Math.PI * 1.5, 0);
            ctx.Arc(x + w - r, y + h - r, r, 0,             Math.PI * 0.5);
            ctx.Arc(x + r,     y + h - r, r, Math.PI * 0.5, Math.PI);
            ctx.ClosePath();
        }
    }
}
