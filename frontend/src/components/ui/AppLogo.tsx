import { useTheme } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

const MARK_SIZE = {
  xs: 28,
  sm: 34,
  md: 40,
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

  if (variant === "mark") {
    return (
      <div
        className={cn("relative shrink-0 overflow-hidden", className)}
        style={{ width: mark, height: mark }}
        aria-label="JUICE"
      >
        <img
          src={src}
          alt=""
          className="absolute left-1/2 top-0 -translate-x-1/2 object-contain object-top"
          style={{
            width: mark * 2.1,
            clipPath: "inset(0 0 36% 0)",
            filter:
              mode === "dark"
                ? "drop-shadow(0 0 8px rgba(168, 85, 247, 0.35))"
                : undefined,
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
        className="block w-full max-w-[148px] object-contain"
        style={{
          filter:
            mode === "dark"
              ? "drop-shadow(0 2px 16px rgba(147, 51, 234, 0.22))"
              : "drop-shadow(0 2px 8px rgba(15, 23, 42, 0.08))",
        }}
        draggable={false}
      />
    </div>
  );
}
