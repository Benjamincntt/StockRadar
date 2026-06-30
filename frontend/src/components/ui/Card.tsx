import { cn } from "@/lib/utils";

interface CardProps {
  children: React.ReactNode;
  className?: string;
  padding?: "sm" | "md" | "lg" | "none";
  glass?: boolean;
  wave?: boolean;
}

export function Card({ children, className, padding = "md", glass = true, wave }: CardProps) {
  const pad =
    padding === "none" ? "" : { sm: "p-4", md: "p-5", lg: "p-6" }[padding];

  return (
    <div
      className={cn(
        glass ? "glass-card rounded-2xl" : "card-panel rounded-xl",
        wave && "wave-card",
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
  return (
    <div className="mb-4 flex items-end justify-between gap-3">
      <div>
        <h2 className="text-base font-semibold text-on-surface lg:text-lg">{title}</h2>
        {subtitle && (
          <p
            className="mt-1 text-xs text-on-surface-variant"
            dangerouslySetInnerHTML={{ __html: subtitle }}
          />
        )}
      </div>
      {action}
    </div>
  );
}
