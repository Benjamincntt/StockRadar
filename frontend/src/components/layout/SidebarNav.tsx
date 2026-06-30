import { NavLink } from "react-router-dom";
import { LiveStatusBadge } from "@/components/layout/LiveStatusBadge";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { AppLogo } from "@/components/ui/AppLogo";
import { mainNavLinks } from "@/components/layout/navConfig";
import { useThemeTokens } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

interface SidebarNavProps {
  className?: string;
}

export function SidebarNav({ className }: SidebarNavProps) {
  const theme = useThemeTokens();

  return (
    <aside
      className={cn(
        "flex shrink-0 flex-col border-r border-outline-variant/30 bg-surface-low",
        className,
      )}
    >
      <div className="border-b border-outline-variant/30 px-5 py-5">
        <AppLogo size="md" />
        <div className="mt-3">
          <LiveStatusBadge inline />
        </div>
      </div>

      <nav className="flex-1 space-y-1 overflow-y-auto p-3">
        {mainNavLinks.map((link) => (
          <NavLink
            key={link.to}
            to={link.to}
            end={link.end}
            className={({ isActive }) =>
              cn(
                "flex items-center gap-3 rounded-xl px-3 py-2.5 transition-colors",
                isActive
                  ? "bg-positive-dim text-primary"
                  : "text-on-surface-variant hover:bg-surface-high hover:text-on-surface",
              )
            }
          >
            {({ isActive }) => {
              const Icon = link.icon;
              return (
                <>
                  <span
                    className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg"
                    style={{
                      backgroundColor: isActive ? theme.greenBg : theme.surfaceElevated,
                      color: isActive ? theme.primary : theme.textMuted,
                    }}
                  >
                    <Icon className="h-4 w-4" strokeWidth={isActive ? 2.5 : 2} />
                  </span>
                  <span className="min-w-0">
                    <span className="block text-sm font-semibold leading-tight">{link.label}</span>
                    <span className="mt-0.5 block truncate text-[11px] opacity-80">{link.desc}</span>
                  </span>
                </>
              );
            }}
          </NavLink>
        ))}
      </nav>

      <div className="border-t border-outline-variant/30 px-5 py-4">
        <div className="flex items-center justify-between">
          <span className="text-[10px] text-on-surface-variant">Giao diện</span>
          <ThemeToggle compact />
        </div>
      </div>
    </aside>
  );
}
