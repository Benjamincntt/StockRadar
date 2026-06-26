import { cn } from "@/lib/utils";
import { theme } from "@/theme/tokens";

interface CardProps {
  children: React.ReactNode;
  className?: string;
  padding?: "sm" | "md" | "lg";
}

export function Card({ children, className, padding = "md" }: CardProps) {
  const pad = { sm: "p-3", md: "p-4", lg: "p-5" }[padding];
  return (
    <div
      className={cn("rounded-[20px] border bg-white", pad, className)}
      style={{ borderColor: theme.border, boxShadow: theme.shadow }}
    >
      {children}
    </div>
  );
}

interface SectionTitleProps {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
}

export function SectionTitle({ title, subtitle, action }: SectionTitleProps) {
  return (
    <div className="mb-3 flex items-end justify-between gap-2">
      <div>
        <h2 className="text-[15px] font-semibold text-gray-900">{title}</h2>
        {subtitle && <p className="mt-0.5 text-xs text-gray-500">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}
