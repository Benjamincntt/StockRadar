import { cn } from "@/lib/utils";
import { useTheme } from "@/context/ThemeContext";

interface CardProps {
  children: React.ReactNode;
  className?: string;
  padding?: "sm" | "md" | "lg" | "none";
  glass?: boolean;
}

export function Card({ children, className, padding = "md", glass }: CardProps) {
  const { mode } = useTheme();
  const useGlass = glass ?? mode === "dark";
  const pad =
    padding === "none" ? "" : { sm: "p-3", md: "p-4", lg: "p-5" }[padding];

  return (
    <div
      className={cn(
        "card-panel",
        mode === "light" ? "bg-surface-lowest" : "bg-surface",
        useGlass && "glass-reflection",
        pad,
        className,
      )}
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
  const { mode } = useTheme();
  return (
    <div className="mb-3 flex items-end justify-between gap-2">
      <div>
        <h2
          className={cn(
            "text-sm font-bold",
            mode === "light" ? "text-on-surface" : "text-on-surface",
          )}
        >
          {title}
        </h2>
        {subtitle && (
          <p
            className="mt-0.5 text-[10px] text-on-surface-variant"
            dangerouslySetInnerHTML={{ __html: subtitle }}
          />
        )}
      </div>
      {action}
    </div>
  );
}
