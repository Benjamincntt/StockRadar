import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Pencil } from "lucide-react";
import { api } from "@/lib/api";
import type { WatchlistItem } from "@/types";
import { Card } from "@/components/ui/Card";
import { ChangePill, ScorePill } from "@/components/ui/ScorePill";

export function WatchlistPage() {
  const [items, setItems] = useState<WatchlistItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [symbol, setSymbol] = useState("");
  const [sectorOptions, setSectorOptions] = useState<string[]>([]);
  const [editingSymbol, setEditingSymbol] = useState<string | null>(null);
  const [draftSector, setDraftSector] = useState("");
  const [saving, setSaving] = useState(false);

  const load = async () => {
    const rows = await api.getWatchlist();
    setItems(rows);
  };

  useEffect(() => {
    Promise.all([load(), api.getSectorCatalog().then((rows) => setSectorOptions(rows.map((r) => r.name)))])
      .catch(() => undefined)
      .finally(() => setLoading(false));
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
      <div>
        <h1 className="text-xl font-bold text-on-surface">Watchlist</h1>
        <p className="mt-1 text-xs text-on-surface-variant">
          Theo dõi mã · chỉnh ngành thủ công (khóa sau khi lưu)
        </p>
      </div>

      <Card>
        <div className="mb-4 flex gap-2 rounded-full border border-outline-variant/30 bg-surface-lowest p-1.5 shadow-sm">
          <input
            value={symbol}
            onChange={(e) => setSymbol(e.target.value)}
            placeholder="Nhập mã (VD: SSI)"
            className="input-obsidian flex-1 rounded-full border-0 bg-transparent px-4 py-2.5 text-sm text-on-surface shadow-none focus:ring-0"
          />
          <button
            type="button"
            onClick={add}
            className="rounded-full bg-primary px-5 py-2.5 text-sm font-bold text-on-primary"
          >
            Thêm
          </button>
        </div>

        {items.length > 0 && (
          <div className="mb-2 grid grid-cols-12 gap-2 px-2 text-[10px] font-bold uppercase tracking-wide text-on-surface-variant">
            <span className="col-span-4">Mã / Ngành</span>
            <span className="col-span-2 text-center">Điểm</span>
            <span className="col-span-3 text-center">Phiên trước</span>
            <span className="col-span-1 text-center">Sửa</span>
            <span className="col-span-2 text-right">Xóa</span>
          </div>
        )}

        <div className="space-y-1.5">
          {loading && (
            <p className="px-1 py-6 text-center text-sm text-on-surface-variant">Đang tải watchlist...</p>
          )}
          {!loading && items.length === 0 && (
            <p className="px-1 py-6 text-center text-sm text-on-surface-variant">
              Chưa có mã trong watchlist.
            </p>
          )}
          {items.map((item) => (
            <div
              key={item.symbol}
              className="grid grid-cols-12 items-center gap-2 rounded-xl bg-surface-low px-2 py-3"
            >
              <div className="col-span-4 min-w-0">
                <Link to={`/stocks/${item.symbol}`} className="block">
                  <p className="text-sm font-bold text-on-surface">{item.symbol}</p>
                </Link>
                {editingSymbol === item.symbol ? (
                  <div className="mt-1 flex flex-col gap-2">
                    <select
                      value={draftSector}
                      onChange={(e) => setDraftSector(e.target.value)}
                      className="input-obsidian w-full rounded-lg px-2 py-1.5 text-xs text-on-surface"
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
                        className="rounded-lg bg-primary px-2 py-1 text-xs font-semibold text-on-primary disabled:opacity-50"
                      >
                        Lưu
                      </button>
                      <button
                        type="button"
                        onClick={closeSectorEdit}
                        className="rounded-lg px-2 py-1 text-xs text-on-surface-variant"
                      >
                        Hủy
                      </button>
                    </div>
                  </div>
                ) : (
                  <p className="truncate text-xs text-on-surface-variant">
                    {item.sector || "Chưa phân ngành"}
                    {item.sectorLocked && (
                      <span className="ml-1 text-[10px] text-warning">· đã khóa</span>
                    )}
                  </p>
                )}
              </div>
              <div className="col-span-2 flex justify-center">
                <ScorePill score={item.score} />
              </div>
              <div className="col-span-3 flex justify-center">
                <ChangePill value={item.changePercent} />
              </div>
              <div className="col-span-1 flex justify-center">
                <button
                  type="button"
                  onClick={() => openSectorEdit(item)}
                  className="rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-high hover:text-on-surface"
                  aria-label={`Sửa ngành ${item.symbol}`}
                  title="Sửa ngành"
                >
                  <Pencil className="h-4 w-4" />
                </button>
              </div>
              <div className="col-span-2 text-right">
                <button
                  type="button"
                  onClick={() => remove(item.symbol)}
                  className="text-xs font-medium text-negative"
                >
                  Xóa
                </button>
              </div>
            </div>
          ))}
        </div>
      </Card>

      {editingSymbol && (
        <button
          type="button"
          className="fixed inset-0 z-10 bg-black/40 sm:hidden"
          onClick={closeSectorEdit}
          aria-label="Đóng"
        />
      )}
    </div>
  );
}
