import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { LiveMarketProvider } from "@/context/LiveMarketContext";
import { MobileShell } from "@/components/layout/MobileShell";
import { HomePage } from "@/pages/HomePage";
import { StockDetailPage } from "@/pages/StockDetailPage";
import { AlertsPage } from "@/pages/AlertsPage";
import { WatchlistPage } from "@/pages/WatchlistPage";
import { LoginPage } from "@/pages/LoginPage";
import { JobsPage } from "@/pages/JobsPage";
import { CriteriaSummaryPage } from "@/pages/CriteriaSummaryPage";

export default function App() {
  return (
    <LiveMarketProvider>
      <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={
            <MobileShell>
              <Routes>
                <Route path="/" element={<HomePage />} />
                <Route path="/radar" element={<Navigate to="/" replace />} />
                <Route path="/stocks/:symbol" element={<StockDetailPage />} />
                <Route path="/alerts" element={<AlertsPage />} />
                <Route path="/watchlist" element={<WatchlistPage />} />
                <Route path="/heatmap" element={<Navigate to="/" replace />} />
                <Route path="/jobs" element={<JobsPage />} />
                <Route path="/criteria" element={<CriteriaSummaryPage />} />
              </Routes>
            </MobileShell>
          }
        />
      </Routes>
      </BrowserRouter>
    </LiveMarketProvider>
  );
}
