import { Moon, Sun } from "lucide-react";
import { useTheme } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

interface ThemeToggleProps {
  className?: string;
  compact?: boolean;
}

export function ThemeToggle({ className, compact = false }: ThemeToggleProps) {
  const { mode, toggle } = useTheme();
  const isLight = mode === "light";

  return (
    <button
      type="button"
      onClick={toggle}
      className={cn(
        "flex items-center justify-center rounded-full transition-colors",
        compact ? "h-9 w-9" : "h-9 gap-1.5 px-3",
        isLight
          ? "bg-surface-low text-on-surface-variant hover:bg-surface-high hover:text-primary"
          : "bg-surface-high text-on-surface-variant hover:text-primary",
        className,
      )}
      aria-label={isLight ? "Chuyển sang chế độ tối" : "Chuyển sang chế độ sáng"}
      title={isLight ? "Dark mode" : "Light mode"}
    >
      {isLight ? <Moon className="h-4 w-4" /> : <Sun className="h-4 w-4" />}
      {!compact && (
        <span className="text-[10px] font-semibold">{isLight ? "Tối" : "Sáng"}</span>
      )}
    </button>
  );
}
