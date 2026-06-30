import { useTheme } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

const HEIGHT = {
  xs: 24,
  sm: 32,
  md: 44,
  lg: 72,
  xl: 120,
} as const;

type AppLogoSize = keyof typeof HEIGHT;

const LOGO = {
  light: "/juice-logo.png",
  dark: "/juice-logo-dark.png",
} as const;

interface AppLogoProps {
  className?: string;
  size?: AppLogoSize;
  /** Hiển thị chữ JUICE bên cạnh (logo PNG đã có chữ — mặc định chỉ ảnh). */
  withWordmark?: boolean;
}

export function AppLogo({ className, size = "md", withWordmark = false }: AppLogoProps) {
  const { mode } = useTheme();
  const h = HEIGHT[size];
  const src = LOGO[mode];

  return (
    <div className={cn("inline-flex items-center gap-2", className)}>
      <img
        src={src}
        alt="JUICE"
        className="w-auto shrink-0 object-contain"
        style={{ height: h }}
        draggable={false}
      />
      {withWordmark && (
        <span className="text-base font-bold tracking-[0.2em] text-on-surface">JUICE</span>
      )}
    </div>
  );
}
