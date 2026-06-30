import { cn } from "@/lib/utils";

const HEIGHT = {
  xs: 24,
  sm: 32,
  md: 44,
  lg: 72,
  xl: 120,
} as const;

type AppLogoSize = keyof typeof HEIGHT;

interface AppLogoProps {
  className?: string;
  size?: AppLogoSize;
  /** Hiển thị chữ JUICE bên cạnh (logo PNG đã có chữ — mặc định chỉ ảnh). */
  withWordmark?: boolean;
}

export function AppLogo({ className, size = "md", withWordmark = false }: AppLogoProps) {
  const h = HEIGHT[size];

  return (
    <div className={cn("inline-flex items-center gap-2", className)}>
      <img
        src="/juice-logo.png"
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
