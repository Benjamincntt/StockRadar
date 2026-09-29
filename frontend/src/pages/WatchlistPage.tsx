import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { ChevronDown, FolderPlus, Loader2, Pencil, Plus, Trash2, X } from "lucide-react";
import { api } from "@/lib/api";
import type { WatchlistDto, WatchlistItem } from "@/types";
import { Card } from "@/components/ui/Card";
import { ChangePill, ScorePill } from "@/components/ui/ScorePill";

type Dialog =
  | { kind: "create" }
  | { kind: "rename"; watchlist: WatchlistDto }
  | { kind: "delete"; watchlist: WatchlistDto }
  | null;

export function WatchlistPage() {
  const [watchlists, setWatchlists] = useState<WatchlistDto[]>([]);
  const [listsLoading, setListsLoading] = useState(true);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [items, setItems] = useState<WatchlistItem[]>([]);
  const [itemsLoading, setItemsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [dialogName, setDialogName] = useState("");
  const [dialogBusy, setDialogBusy] = useState(false);
  const [addSymbol, setAddSymbol] = useState("");
  const [adding, setAdding] = useState(false);
  const [sectorsOpen, setSectorsOpen] = useState(false);

  const selected = useMemo(
    () => watchlists.find((w) => w.id === selectedId) ?? null,
    [watchlists, selectedId],
  );

  const loadLists = useCallback(async (preferId?: number) => {
    setListsLoading(true);
    try {
      const lists = await api.getWatchlists();
      setWatchlists(lists);
      setSelectedId((prev) => {
        if (preferId != null && lists.some((w) => w.id === preferId)) return preferId;
        if (prev != null && lists.some((w) => w.id === prev)) return prev;
        return lists.find((w) => w.laMacDinh)?.id ?? lists[0]?.id ?? null;
      });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không tải được danh sách watchlist.");
    } finally {
      setListsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadLists();
  }, [loadLists]);

  useEffect(() => {
    if (selectedId == null) {
      setItems([]);
      return;
    }
    let cancelled = false;
    setItemsLoading(true);
    setError(null);
    api
      .getWatchlistItems(selectedId)
      .then((rows) => {
        if (!cancelled) setItems(rows);
      })
      .catch((e) => {
        if (!cancelled) {
          setItems([]);
          setError(e instanceof Error ? e.message : "Không tải được mã trong danh sách.");
        }
      })
      .finally(() => {
        if (!cancelled) setItemsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [selectedId]);

  const danhSachCuaToi = useMemo(
    () => watchlists.filter((w) => !w.laDanhSachNganh),
    [watchlists],
  );
  const theoNganh = useMemo(
    () => watchlists.filter((w) => w.laDanhSachNganh),
    [watchlists],
  );
  const coTheChinhSua = selected != null && !selected.laDanhSachNganh;

  const openDialog = (d: Exclude<Dialog, null>) => {
    setDialog(d);
    setDialogName(d.kind === "rename" ? d.watchlist.name : "");
  };
  const closeDialog = () => {
    setDialog(null);
    setDialogName("");
  };

  const submitDialog = async () => {
    if (!dialog || !dialogName.trim() || dialogBusy) return;
    setDialogBusy(true);
    setError(null);
    try {
      if (dialog.kind === "create") {
        const created = await api.createWatchlist(dialogName.trim());
        closeDialog();
        await loadLists(created.id);
      } else if (dialog.kind === "rename") {
        await api.renameWatchlist(dialog.watchlist.id, dialogName.trim());
        closeDialog();
        await loadLists(dialog.watchlist.id);
      } else {
        await api.deleteWatchlist(dialog.watchlist.id);
        closeDialog();
        await loadLists();
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Thao tác thất bại.");
    } finally {
      setDialogBusy(false);
    }
  };

  const addSymbolToSelected = async () => {
    const code = addSymbol.trim().toUpperCase();
    if (!code || selectedId == null || adding) return;
    setAdding(true);
    setError(null);
    try {
      await api.addToWatchlistById(selectedId, code);
      setAddSymbol("");
      setItems(await api.getWatchlistItems(selectedId));
      setWatchlists((prev) =>
        prev.map((w) => (w.id === selectedId ? { ...w, soLuongMa: w.soLuongMa + 1 } : w)),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thêm được mã.");
    } finally {
      setAdding(false);
    }
  };

  const removeSymbol = async (symbol: string) => {
    if (selectedId == null) return;
    setError(null);
    try {
      await api.removeFromWatchlistById(selectedId, symbol);
      setItems((prev) => prev.filter((it) => it.symbol !== symbol));
      setWatchlists((prev) =>
        prev.map((w) =>
          w.id === selectedId ? { ...w, soLuongMa: Math.max(0, w.soLuongMa - 1) } : w,
        ),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không xóa được mã.");
    }
  };

  const WatchlistRow = ({ w }: { w: WatchlistDto }) => {
    const active = w.id === selectedId;
    return (
      <div
        className={`group flex items-center gap-1 ${active ? "rounded-xl bg-primary/10" : ""}`}
      >
        <button
          type="button"
          onClick={() => setSelectedId(w.id)}
          className={`flex min-w-0 flex-1 items-center justify-between gap-2 rounded-xl px-3 py-2 text-left text-sm transition-colors ${
            active
              ? "font-bold text-primary"
              : "text-on-surface hover:bg-surface-high"
          }`}
        >
          <span className="truncate">
            {w.name}
            {w.laMacDinh && <span className="ml-1 text-[10px] text-on-surface-variant">· mặc định</span>}
          </span>
          <span
            className={`shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-bold ${
              active ? "bg-primary/20 text-primary" : "bg-surface-high text-on-surface-variant"
            }`}
          >
            {w.soLuongMa}
          </span>
        </button>
        {!w.laDanhSachNganh && !w.laMacDinh && (
          <div className="flex shrink-0 items-center pr-1 opacity-0 transition-opacity group-hover:opacity-100 focus-within:opacity-100">
            <button
              type="button"
              onClick={() => openDialog({ kind: "rename", watchlist: w })}
              className="rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-high hover:text-on-surface"
              aria-label={`Đổi tên ${w.name}`}
              title="Đổi tên"
            >
              <Pencil className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              onClick={() => openDialog({ kind: "delete", watchlist: w })}
              className="rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-high hover:text-negative"
              aria-label={`Xóa ${w.name}`}
              title="Xóa danh sách"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
        )}
      </div>
    );
  };

  const Chip = ({ w }: { w: WatchlistDto }) => {
    const active = w.id === selectedId;
    return (
      <button
        type="button"
        onClick={() => setSelectedId(w.id)}
        className={`flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors ${
          active
            ? "border-primary bg-primary/10 text-primary"
            : "border-outline-variant/40 bg-surface-low text-on-surface-variant"
        }`}
      >
        {w.name}
        <span className="rounded-full bg-surface-high px-1.5 text-[10px] font-bold">
          {w.soLuongMa}
        </span>
      </button>
    );
  };

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-bold text-on-surface">Watchlist</h1>
        <p className="mt-1 text-xs text-on-surface-variant">
          Quản lý nhiều danh sách theo dõi · mặc định, tự tạo và tự động theo ngành
        </p>
      </div>

      {error && (
        <div className="flex items-start justify-between gap-3 rounded-xl border border-negative/30 bg-negative/10 px-4 py-3 text-sm text-negative">
          <span>{error}</span>
          <button type="button" onClick={() => setError(null)} aria-label="Đóng thông báo lỗi">
            <X className="h-4 w-4" />
          </button>
        </div>
      )}

      {/* Mobile: chips ngang */}
      <div className="md:hidden space-y-2">
        <div className="flex items-center justify-between">
          <h2 className="text-xs font-bold uppercase tracking-wide text-on-surface-variant">
            Danh sách của tôi
          </h2>
          <button
            type="button"
            onClick={() => openDialog({ kind: "create" })}
            className="flex items-center gap-1 rounded-full bg-primary px-3 py-1 text-xs font-bold text-on-primary"
          >
            <Plus className="h-3.5 w-3.5" /> Tạo
          </button>
        </div>
        <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
          {listsLoading
            ? [0, 1, 2].map((i) => (
                <div
                  key={i}
                  className="h-8 w-24 shrink-0 animate-pulse rounded-full bg-surface-high"
                />
              ))
            : danhSachCuaToi.map((w) => <Chip key={w.id} w={w} />)}
        </div>
        {!listsLoading && theoNganh.length > 0 && (
          <>
            <h2 className="pt-1 text-xs font-bold uppercase tracking-wide text-on-surface-variant">
              Theo ngành
            </h2>
            <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
              {theoNganh.map((w) => (
                <Chip key={w.id} w={w} />
              ))}
            </div>
          </>
        )}
      </div>

      <div className="md:grid md:grid-cols-[260px_1fr] md:gap-4">
        {/* Desktop: sidebar */}
        <Card className="hidden md:block md:self-start">
          <button
            type="button"
            onClick={() => openDialog({ kind: "create" })}
            className="mb-3 flex w-full items-center justify-center gap-2 rounded-full bg-primary px-4 py-2.5 text-sm font-bold text-on-primary transition-opacity hover:opacity-90"
          >
            <FolderPlus className="h-4 w-4" /> Tạo danh sách
          </button>

          {listsLoading ? (
            <div className="space-y-2 py-2">
              {[0, 1, 2, 3].map((i) => (
                <div key={i} className="h-8 animate-pulse rounded-xl bg-surface-high" />
              ))}
            </div>
          ) : (
            <div className="space-y-3">
              <div>
                <p className="mb-1 px-3 text-[10px] font-bold uppercase tracking-wide text-on-surface-variant">
                  Danh sách của tôi
                </p>
                <div className="space-y-0.5">
                  {danhSachCuaToi.map((w) => (
                    <WatchlistRow key={w.id} w={w} />
                  ))}
                </div>
              </div>
              {theoNganh.length > 0 && (
                <div>
                  <button
                    type="button"
                    onClick={() => setSectorsOpen((v) => !v)}
                    className="flex w-full items-center justify-between px-3 py-1 text-[10px] font-bold uppercase tracking-wide text-on-surface-variant hover:text-on-surface"
                  >
                    Theo ngành
                    <ChevronDown
                      className={`h-3.5 w-3.5 transition-transform ${sectorsOpen ? "" : "-rotate-90"}`}
                    />
                  </button>
                  {sectorsOpen && (
                    <div className="mt-1 space-y-0.5">
                      {theoNganh.map((w) => (
                        <WatchlistRow key={w.id} w={w} />
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </Card>

        {/* Main: bảng mã */}
        <Card>
          <div className="mb-3 flex items-center justify-between gap-2">
            <div className="min-w-0">
              <h2 className="truncate text-base font-bold text-on-surface">
                {selected?.name ?? "—"}
              </h2>
              {selected?.laDanhSachNganh && (
                <p className="mt-0.5 text-xs text-on-surface-variant">
                  Danh sách tự động theo ngành — không thể thêm/xóa mã thủ công
                </p>
              )}
            </div>
          </div>

          {coTheChinhSua && (
            <div className="mb-4 flex gap-2 rounded-full border border-outline-variant/30 bg-surface-lowest p-1.5 shadow-sm">
              <input
                value={addSymbol}
                onChange={(e) => setAddSymbol(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") void addSymbolToSelected();
                }}
                placeholder="Nhập mã (VD: SSI)"
                className="input-obsidian flex-1 rounded-full border-0 bg-transparent px-4 py-2.5 text-sm text-on-surface shadow-none focus:ring-0"
              />
              <button
                type="button"
                onClick={addSymbolToSelected}
                disabled={adding || !addSymbol.trim()}
                className="flex items-center gap-1 rounded-full bg-primary px-5 py-2.5 text-sm font-bold text-on-primary disabled:opacity-50"
              >
                {adding ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
                Thêm
              </button>
            </div>
          )}

          {items.length > 0 && (
            <div className="mb-2 grid grid-cols-12 gap-2 px-2 text-[10px] font-bold uppercase tracking-wide text-on-surface-variant">
              <span className="col-span-4">Mã</span>
              <span className="col-span-3 hidden sm:block">Ngành</span>
              <span className="col-span-2 text-center">Điểm</span>
              <span className="col-span-2 text-center sm:col-span-1">Thay đổi %</span>
              <span className="col-span-2 text-right sm:col-span-1">
                {coTheChinhSua ? "Xóa" : ""}
              </span>
            </div>
          )}

          <div className="space-y-1.5">
            {itemsLoading && (
              <div className="space-y-1.5 py-2">
                {[0, 1, 2, 3, 4].map((i) => (
                  <div
                    key={i}
                    className="h-12 animate-pulse rounded-xl bg-surface-low opacity-70"
                  />
                ))}
              </div>
            )}
            {!itemsLoading && items.length === 0 && selected && (
              <p className="px-1 py-8 text-center text-sm text-on-surface-variant">
                {selected.laDanhSachNganh
                  ? "Chưa có mã nào trong ngành này."
                  : "Chưa có mã nào. Thêm mã để theo dõi."}
              </p>
            )}
            {!itemsLoading &&
              !selected &&
              !listsLoading &&
              watchlists.length === 0 && (
                <p className="px-1 py-8 text-center text-sm text-on-surface-variant">
                  Chưa có danh sách nào.
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
                    <p className="truncate text-xs text-on-surface-variant">{item.name}</p>
                  </Link>
                </div>
                <div className="col-span-3 hidden min-w-0 sm:block">
                  <p className="truncate text-xs text-on-surface-variant">
                    {item.sector || "Chưa phân ngành"}
                  </p>
                </div>
                <div className="col-span-2 flex justify-center">
                  <ScorePill score={item.score} />
                </div>
                <div className="col-span-2 flex justify-center sm:col-span-1">
                  <ChangePill value={item.changePercent} />
                </div>
                <div className="col-span-2 text-right sm:col-span-1">
                  {coTheChinhSua && (
                    <button
                      type="button"
                      onClick={() => removeSymbol(item.symbol)}
                      className="rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-high hover:text-negative"
                      aria-label={`Xóa ${item.symbol} khỏi danh sách`}
                      title="Xóa khỏi danh sách"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </Card>
      </div>

      {/* Dialog tạo / đổi tên / xóa */}
      {dialog && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/50 p-4">
          <Card className="w-full max-w-sm">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="text-base font-bold text-on-surface">
                {dialog.kind === "create" && "Tạo danh sách mới"}
                {dialog.kind === "rename" && "Đổi tên danh sách"}
                {dialog.kind === "delete" && "Xóa danh sách"}
              </h3>
              <button
                type="button"
                onClick={closeDialog}
                className="rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-high hover:text-on-surface"
                aria-label="Đóng"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {dialog.kind === "delete" ? (
              <p className="text-sm text-on-surface-variant">
                Bạn có chắc muốn xóa danh sách{" "}
                <span className="font-bold text-on-surface">{dialog.watchlist.name}</span>? Thao tác
                này không thể hoàn tác.
              </p>
            ) : (
              <input
                autoFocus
                value={dialogName}
                onChange={(e) => setDialogName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") void submitDialog();
                }}
                placeholder="Tên danh sách (VD: Ngân hàng đầu tư)"
                className="input-obsidian w-full rounded-xl px-4 py-2.5 text-sm text-on-surface"
              />
            )}

            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={closeDialog}
                className="rounded-full px-4 py-2 text-sm font-medium text-on-surface-variant hover:bg-surface-high"
              >
                Hủy
              </button>
              {dialog.kind === "delete" ? (
                <button
                  type="button"
                  onClick={submitDialog}
                  disabled={dialogBusy}
                  className="flex items-center gap-1.5 rounded-full bg-negative px-4 py-2 text-sm font-bold text-white disabled:opacity-50"
                >
                  {dialogBusy && <Loader2 className="h-4 w-4 animate-spin" />}
                  Xóa danh sách
                </button>
              ) : (
                <button
                  type="button"
                  onClick={submitDialog}
                  disabled={dialogBusy || !dialogName.trim()}
                  className="flex items-center gap-1.5 rounded-full bg-primary px-4 py-2 text-sm font-bold text-on-primary disabled:opacity-50"
                >
                  {dialogBusy && <Loader2 className="h-4 w-4 animate-spin" />}
                  {dialog.kind === "create" ? "Tạo" : "Lưu"}
                </button>
              )}
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
