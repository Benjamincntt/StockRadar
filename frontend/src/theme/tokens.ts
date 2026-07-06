import { designDarkColors } from "./design-dark";
import { designLightColors } from "./design-light";

export type ThemeMode = "light" | "dark";

type Palette = typeof designDarkColors | typeof designLightColors;

function buildFromPalette(c: Palette, mode: ThemeMode) {
  const isLight = mode === "light";
  const positiveRgb = isLight ? "0, 109, 65" : "0, 242, 255";
  const negativeRgb = isLight ? "186, 26, 26" : "255, 107, 74";
  const warningRgb = isLight ? "180, 83, 9" : "206, 93, 255";

  return {
    maxWidth: "430px",
    mode,

    bg: c.background,
    surface: c.surfaceContainer,
    surfaceMuted: c.surfaceContainerLow,
    surfaceElevated: c.surfaceContainerHigh,
    surfaceCard: c.surfaceContainerLowest,

    border: c.outlineVariant,
    borderSubtle: isLight ? "#f1f5f9" : "rgba(255, 255, 255, 0.08)",

    text: c.onSurface,
    textMuted: c.onSurfaceVariant,
    textSubtle: c.outline,

    primary: c.primary,
    onPrimary: c.onPrimary,
    primaryContainer: c.primaryContainer,
    secondary: c.secondary,

    green: c.primary,
    greenBg: `rgba(${positiveRgb}, 0.1)`,
    greenSoft: `rgba(${positiveRgb}, ${isLight ? 0.06 : 0.06})`,

    red: c.error,
    redBg: `rgba(${negativeRgb}, 0.12)`,
    redSoft: `rgba(${negativeRgb}, 0.08)`,

    pink: isLight ? "#c44569" : "#ffb4c8",
    pinkBg: isLight ? "rgba(255, 192, 203, 0.45)" : "rgba(255, 150, 170, 0.2)",
    alertBuyBg: isLight ? "rgba(218, 235, 227, 0.72)" : "rgba(0, 242, 255, 0.1)",
    alertSellBg: isLight ? "rgba(255, 218, 218, 0.78)" : "rgba(255, 107, 74, 0.14)",

    blue: c.primary,
    blueBg: `rgba(${positiveRgb}, 0.1)`,

    amber: c.warning,
    amberBg: `rgba(${warningRgb}, 0.12)`,

    neutralBg: isLight ? c.surfaceContainerLow : "rgba(255,255,255,0.05)",

    cardShadow: isLight
      ? "0 4px 6px -1px rgba(15, 23, 42, 0.05)"
      : "none",

    radius: "0.5rem",
    radiusLg: "0.75rem",
    radiusXl: "1rem",
    radiusPill: "9999px",

    shadow: "none",
  } as const;
}

export function buildThemeTokens(mode: ThemeMode) {
  const palette = mode === "light" ? designLightColors : designDarkColors;
  return buildFromPalette(palette, mode);
}

/** Static dark tokens — prefer useThemeTokens() in components */
export const theme = buildThemeTokens("dark");

export { designDarkColors, designLightColors };
