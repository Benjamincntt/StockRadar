import { useTheme } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

const MARK_SIZE = {
  xs: 28,
  sm: 36,
  md: 44,
} as const;

const FULL_MAX = {
  sm: "max-w-[140px]",
  md: "max-w-[168px]",
  lg: "max-w-[220px]",
  xl: "max-w-[min(100%,360px)]",
} as const;

const LOGO = {
  light: "/juice-logo.png",
  dark: "/juice-logo-dark.png",
} as const;

type AppLogoMarkSize = keyof typeof MARK_SIZE;
type AppLogoFullSize = keyof typeof FULL_MAX;

interface AppLogoProps {
  className?: string;
  variant?: "mark" | "full";
  size?: AppLogoMarkSize | AppLogoFullSize;
}

function isMarkSize(size: AppLogoMarkSize | AppLogoFullSize): size is AppLogoMarkSize {
  return size in MARK_SIZE;
}

export function AppLogo({
  className,
  variant = "full",
  size = "md",
}: AppLogoProps) {
  const { mode } = useTheme();
  const src = LOGO[mode];
  const isDark = mode === "dark";

  if (variant === "mark") {
    const mark = isMarkSize(size) ? MARK_SIZE[size] : MARK_SIZE.sm;
    return (
      <img
        src={src}
        alt="JUICE"
        className={cn("mx-auto block w-auto shrink-0 object-contain object-center", className)}
        style={{
          height: mark,
          filter: isDark ? "drop-shadow(0 0 10px rgba(0, 242, 255, 0.35))" : undefined,
        }}
        draggable={false}
      />
    );
  }

  const fullMax = isMarkSize(size) ? FULL_MAX.md : FULL_MAX[size];

  return (
    <div className="flex w-full items-center justify-center">
      <img
        src={src}
        alt="JUICE"
        className={cn("mx-auto block h-auto w-full object-contain object-center", fullMax, className)}
        style={{
          filter: isDark
            ? "drop-shadow(0 0 28px rgba(0, 242, 255, 0.22))"
            : "drop-shadow(0 4px 14px rgba(15, 23, 42, 0.14))",
        }}
        draggable={false}
      />
    </div>
  );
}
