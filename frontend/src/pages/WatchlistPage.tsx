import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Pencil } from "lucide-react";
import { api } from "@/lib/api";
import type { WatchlistItem } from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { ChangePill, ScorePill } from "@/components/ui/ScorePill";
import { theme } from "@/theme/tokens";

export function WatchlistPage() {
  const [items, setItems] = useState<WatchlistItem[]>([]);
  const [symbol, setSymbol] = useState("");
  const [sectorOptions, setSectorOptions] = useState<string[]>([]);
  const [editingSymbol, setEditingSymbol] = useState<string | null>(null);
  const [draftSector, setDraftSector] = useState("");
  const [saving, setSaving] = useState(false);

  const load = () => api.getWatchlist().then(setItems);

  useEffect(() => {
    load();
    api.getSectorCatalog().then((rows) => setSectorOptions(rows.map((r) => r.name)));
  }, []);

  const add = async () => {
    if (!symbol.trim()) return;
    await api.addToWatchlist(symbol.trim().toUpperCase());
    setSymbol("");
    await load();
  };

  const remove = async (code: string) => {
    await api.removeFromWatchlist(code);
    await load();
  };

  const openSectorEdit = (item: WatchlistItem) => {
    setEditingSymbol(item.symbol);
    setDraftSector(item.sector);
  };

  const closeSectorEdit = () => {
    setEditingSymbol(null);
    setDraftSector("");
  };

  const saveSector = async () => {
    if (!editingSymbol || !draftSector.trim()) return;
    setSaving(true);
    try {
      await api.updateStockSector(editingSymbol, draftSector.trim());
      closeSectorEdit();
      await load();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <Card>
        <SectionTitle
          title="Watchlist"
          subtitle="Theo dõi mã · chỉnh ngành thủ công (khóa sau khi lưu)"
        />
        <div className="mb-4 flex gap-2">
          <input
            value={symbol}
            onChange={(e) => setSymbol(e.target.value)}
            placeholder="Nhập mã (VD: SSI)"
            className="flex-1 rounded-2xl border bg-white px-4 py-3 text-sm outline-none focus:ring-2"
            style={{ borderColor: theme.border }}
          />
          <button
            type="button"
            onClick={add}
            className="rounded-2xl px-4 py-3 text-sm font-semibold text-white"
            style={{ backgroundColor: theme.green }}
          >
            Thêm
          </button>
        </div>

        {items.length > 0 && (
          <div
            className="mb-2 hidden grid-cols-[1fr_auto_auto_auto_auto] gap-3 px-3 text-xs font-medium text-gray-400 sm:grid"
          >
            <span>Mã / Ngành</span>
            <span className="w-12 text-center">Điểm</span>
            <span className="w-16 text-center">Phiên trước</span>
            <span className="w-8" />
            <span className="w-10" />
          </div>
        )}

        <div className="space-y-2">
          {items.length === 0 && (
            <p className="px-1 py-6 text-center text-sm text-gray-500">Chưa có mã trong watchlist.</p>
          )}
          {items.map((item) => (
            <div
              key={item.symbol}
              className="grid grid-cols-[1fr_auto_auto_auto_auto] items-center gap-2 rounded-2xl bg-gray-50 px-3 py-3 sm:gap-3"
            >
              <div className="min-w-0">
                <Link to={`/stocks/${item.symbol}`} className="block">
                  <p className="text-sm font-bold text-gray-900">{item.symbol}</p>
                </Link>
                {editingSymbol === item.symbol ? (
                  <div className="mt-1 flex flex-col gap-2 sm:flex-row sm:items-center">
                    <select
                      value={draftSector}
                      onChange={(e) => setDraftSector(e.target.value)}
                      className="w-full rounded-xl border bg-white px-2 py-1.5 text-xs outline-none focus:ring-2 sm:max-w-[220px]"
                      style={{ borderColor: theme.border }}
                    >
                      <option value="">Chọn ngành...</option>
                      {sectorOptions.map((name) => (
                        <option key={name} value={name}>
                          {name}
                        </option>
                      ))}
                    </select>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={saveSector}
                        disabled={saving || !draftSector}
                        className="rounded-lg px-2 py-1 text-xs font-semibold text-white disabled:opacity-50"
                        style={{ backgroundColor: theme.green }}
                      >
                        Lưu
                      </button>
                      <button
                        type="button"
                        onClick={closeSectorEdit}
                        className="rounded-lg px-2 py-1 text-xs text-gray-500"
                      >
                        Hủy
                      </button>
                    </div>
                  </div>
                ) : (
                  <p className="truncate text-xs text-gray-500">
                    {item.sector || "Chưa phân ngành"}
                    {item.sectorLocked && (
                      <span className="ml-1 text-[10px] text-amber-600">· đã khóa</span>
                    )}
                  </p>
                )}
              </div>
              <ScorePill score={item.score} className="justify-self-center" />
              <ChangePill value={item.changePercent} className="justify-self-center" />
              <button
                type="button"
                onClick={() => openSectorEdit(item)}
                className="justify-self-center rounded-lg p-1.5 text-gray-400 hover:bg-white hover:text-gray-700"
                aria-label={`Sửa ngành ${item.symbol}`}
                title="Sửa ngành"
              >
                <Pencil className="h-4 w-4" />
              </button>
              <button
                type="button"
                onClick={() => remove(item.symbol)}
                className="justify-self-end text-xs font-medium text-red-500"
              >
                Xóa
              </button>
            </div>
          ))}
        </div>
      </Card>

      {editingSymbol && (
        <button
          type="button"
          className="fixed inset-0 z-10 bg-black/20 sm:hidden"
          onClick={closeSectorEdit}
          aria-label="Đóng"
        />
      )}
    </div>
  );
}
