import { NavLink } from "react-router-dom";
import { LiveStatusBadge } from "@/components/layout/LiveStatusBadge";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { AppLogo } from "@/components/ui/AppLogo";
import { mainNavLinks } from "@/components/layout/navConfig";
import { cn } from "@/lib/utils";

interface SidebarNavProps {
  className?: string;
}

export function SidebarNav({ className }: SidebarNavProps) {
  return (
    <aside
      className={cn(
        "sidebar-shell flex shrink-0 flex-col border-r border-outline-variant/40",
        className,
      )}
    >
      <div className="border-b border-outline-variant/30 px-6 py-8">
        <AppLogo variant="full" />
        <p className="label-caps mt-3 text-center text-on-surface-variant">Smart Money Flow</p>
        <div className="mt-3 flex justify-center">
          <LiveStatusBadge />
        </div>
      </div>

      <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
        {mainNavLinks.map((link) => (
          <NavLink
            key={link.to}
            to={link.to}
            end={link.end}
            className={({ isActive }) => cn(isActive ? "nav-item-active" : "nav-item")}
          >
            {({ isActive }) => {
              const Icon = link.icon;
              return (
                <>
                  <Icon
                    className="h-5 w-5 shrink-0"
                    strokeWidth={isActive ? 2.5 : 2}
                    fill={isActive && link.filledWhenActive ? "currentColor" : "none"}
                  />
                  <span className="min-w-0">
                    <span className="block leading-tight">{link.label}</span>
                    <span className="mt-0.5 block truncate text-[11px] font-normal opacity-70">
                      {link.desc}
                    </span>
                  </span>
                </>
              );
            }}
          </NavLink>
        ))}
      </nav>

      <div className="border-t border-outline-variant/30 px-6 py-4">
        <div className="flex items-center justify-between">
          <span className="text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
            Giao diện
          </span>
          <ThemeToggle compact />
        </div>
      </div>
    </aside>
  );
}
