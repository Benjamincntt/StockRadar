import { TopBar } from "./TopBar";
import { BottomNav } from "./BottomNav";

interface MobileShellProps {
  children: React.ReactNode;
}

export function MobileShell({ children }: MobileShellProps) {
  return (
    <div className="flex min-h-screen flex-col bg-background wave-bg">
      <TopBar />

      <main className="flex-1 overflow-y-auto px-4 py-5 pb-24 lg:px-10 lg:py-8 lg:pb-10">
        <div className="page-container">{children}</div>
      </main>

      <BottomNav />
    </div>
  );
}
