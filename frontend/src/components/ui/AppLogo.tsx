import { useTheme } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

const MARK_SIZE = {
  xs: 28,
  sm: 36,
  md: 44,
} as const;

type AppLogoMarkSize = keyof typeof MARK_SIZE;

const LOGO = {
  light: "/juice-logo.png",
  dark: "/juice-logo-dark.png",
} as const;

interface AppLogoProps {
  className?: string;
  /** Icon only — header / mobile */
  variant?: "mark" | "full";
  size?: AppLogoMarkSize;
}

export function AppLogo({ className, variant = "full", size = "sm" }: AppLogoProps) {
  const { mode } = useTheme();
  const src = LOGO[mode];
  const mark = MARK_SIZE[size];
  const darkBlend = mode === "dark";

  if (variant === "mark") {
    return (
      <div
        className={cn("relative shrink-0 overflow-hidden rounded-lg", className)}
        style={{ width: mark, height: mark }}
        aria-label="JUICE"
      >
        <img
          src={src}
          alt=""
          className={cn(
            "absolute left-1/2 top-0 -translate-x-1/2 object-contain object-top",
            darkBlend && "mix-blend-lighten",
          )}
          style={{
            width: mark * 2.05,
            clipPath: "inset(0 0 34% 0)",
          }}
          draggable={false}
        />
      </div>
    );
  }

  return (
    <div className={cn("flex w-full justify-center", className)}>
      <img
        src={src}
        alt="JUICE"
        className={cn(
          "block w-full max-w-[168px] object-contain",
          darkBlend && "mix-blend-lighten",
        )}
        draggable={false}
      />
    </div>
  );
}
