import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { LiveMarketProvider } from "@/context/LiveMarketContext";
import { ThemeProvider } from "@/context/ThemeContext";
import { MobileShell } from "@/components/layout/MobileShell";
import { HomePage } from "@/pages/HomePage";
import { StockDetailPage } from "@/pages/StockDetailPage";
import { RightsEventsPage } from "@/pages/RightsEventsPage";
import { HieuQuaPage } from "@/pages/HieuQuaPage";
import { WatchlistPage } from "@/pages/WatchlistPage";
import { LoginPage } from "@/pages/LoginPage";

export default function App() {
  return (
    <ThemeProvider>
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
                <Route path="/stocks/:symbol/su-kien-quyen" element={<RightsEventsPage />} />
                <Route path="/watchlist" element={<WatchlistPage />} />
                <Route path="/performance" element={<HieuQuaPage />} />
                <Route path="/heatmap" element={<Navigate to="/" replace />} />
              </Routes>
            </MobileShell>
          }
        />
      </Routes>
      </BrowserRouter>
      </LiveMarketProvider>
    </ThemeProvider>
  );
}
