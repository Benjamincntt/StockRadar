import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ChevronLeft } from "lucide-react";
import { api } from "@/lib/api";
import type { RightsEvent } from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";

export function RightsEventsPage() {
  const { symbol = "" } = useParams();
  const ma = symbol.toUpperCase();
  const [danhSach, setDanhSach] = useState<RightsEvent[]>([]);
  const [loi, setLoi] = useState<string | null>(null);
  const [dangTai, setDangTai] = useState(true);
  const [dangLuu, setDangLuu] = useState(false);
  const [ngay, setNgay] = useState("");
  const [tienMat, setTienMat] = useState("0");
  const [heSoPhaLoang, setHeSoPhaLoang] = useState("1");
  const [soCoCu, setSoCoCu] = useState("0");
  const [soCoMoi, setSoCoMoi] = useState("0");
  const [giaPhatHanh, setGiaPhatHanh] = useState("0");

  const taiDanhSach = () => {
    if (!ma) return;
    setDangTai(true);
    api
      .getRightsEvents(ma)
      .then(setDanhSach)
      .catch((e) => setLoi(e instanceof Error ? e.message : "Không tải được sự kiện quyền."))
      .finally(() => setDangTai(false));
  };

  useEffect(() => {
    taiDanhSach();
  }, [ma]);

  const them = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoi(null);
    setDangLuu(true);
    try {
      await api.addRightsEvent(ma, {
        exDate: ngay,
        cash: Number(tienMat),
        dilution: Number(heSoPhaLoang),
        oldShares: Number(soCoCu) || 0,
        newShares: Number(soCoMoi) || 0,
        issuePrice: Number(giaPhatHanh) || 0,
      });
      setNgay("");
      setTienMat("0");
      setHeSoPhaLoang("1");
      setSoCoCu("0");
      setSoCoMoi("0");
      setGiaPhatHanh("0");
      taiDanhSach();
    } catch (err) {
      setLoi(err instanceof Error ? err.message : "Không lưu được sự kiện.");
    } finally {
      setDangLuu(false);
    }
  };

  return (
    <div className="space-y-4 pb-24 lg:pb-4">
      <div className="flex items-center gap-3">
        <Link
          to={`/stocks/${ma}`}
          className="flex h-9 w-9 items-center justify-center rounded-full bg-surface-high text-on-surface"
          aria-label="Quay lại chi tiết mã"
        >
          <ChevronLeft className="h-5 w-5" />
        </Link>
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-lg font-bold text-on-surface lg:text-2xl">Sự kiện quyền</h2>
          <p className="truncate text-xs text-on-surface-variant lg:text-sm">{ma}</p>
        </div>
      </div>

      <Card>
        <SectionTitle
          title="Đã ghi nhận"
          subtitle="Dùng khi tính % / RS / FOMO. Giá last trên sàn không đổi."
        />
        {dangTai ? (
          <p className="text-sm text-on-surface-variant">Đang tải…</p>
        ) : danhSach.length === 0 ? (
          <p className="text-sm text-on-surface-variant">Chưa có sự kiện cho {ma}.</p>
        ) : (
          <ul className="space-y-2">
            {danhSach.map((sk) => (
              <li
                key={`${sk.symbol}-${sk.exDate}`}
                className="rounded-xl bg-surface-low px-3 py-2 text-sm"
              >
                <p className="font-semibold text-on-surface">GDKHQ {sk.exDate.slice(0, 10)}</p>
                <p className="text-xs text-on-surface-variant">
                  Cổ tức {sk.cash} · pha loãng {sk.dilution}
                  {(sk.newShares ?? 0) > 0
                    ? ` · mua ${sk.oldShares}:${sk.newShares} giá ${sk.issuePrice}`
                    : ""}
                </p>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card>
        <SectionTitle
          title="Thêm sự kiện"
          subtitle="Cổ tức 1.000đ = 1.0 (không ghi 1000). Thưởng 5:1 = pha loãng 1.2. Quyền mua 4:1 giá 10.000đ = 4 / 1 / 10.0."
        />
        <form className="space-y-3" onSubmit={them}>
          <label className="block text-sm">
            <span className="text-on-surface-variant">Ngày không hưởng quyền</span>
            <input
              type="date"
              required
              value={ngay}
              onChange={(e) => setNgay(e.target.value)}
              className="mt-1 w-full rounded-xl bg-surface-low px-3 py-2 text-on-surface"
            />
          </label>
          <label className="block text-sm">
            <span className="text-on-surface-variant">Cổ tức tiền (thang Close)</span>
            <input
              type="number"
              step="0.1"
              min="0"
              value={tienMat}
              onChange={(e) => setTienMat(e.target.value)}
              className="mt-1 w-full rounded-xl bg-surface-low px-3 py-2 text-on-surface"
            />
          </label>
          <label className="block text-sm">
            <span className="text-on-surface-variant">Hệ số pha loãng</span>
            <input
              type="number"
              step="0.01"
              min="0.01"
              value={heSoPhaLoang}
              onChange={(e) => setHeSoPhaLoang(e.target.value)}
              className="mt-1 w-full rounded-xl bg-surface-low px-3 py-2 text-on-surface"
            />
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="block text-sm">
              <span className="text-on-surface-variant">Cổ cũ (n)</span>
              <input
                type="number"
                min="0"
                step="1"
                value={soCoCu}
                onChange={(e) => setSoCoCu(e.target.value)}
                className="mt-1 w-full rounded-xl bg-surface-low px-3 py-2 text-on-surface"
              />
            </label>
            <label className="block text-sm">
              <span className="text-on-surface-variant">Cổ mới mua (m)</span>
              <input
                type="number"
                min="0"
                step="1"
                value={soCoMoi}
                onChange={(e) => setSoCoMoi(e.target.value)}
                className="mt-1 w-full rounded-xl bg-surface-low px-3 py-2 text-on-surface"
              />
            </label>
          </div>
          <label className="block text-sm">
            <span className="text-on-surface-variant">Giá phát hành (thang Close)</span>
            <input
              type="number"
              step="0.1"
              min="0"
              value={giaPhatHanh}
              onChange={(e) => setGiaPhatHanh(e.target.value)}
              className="mt-1 w-full rounded-xl bg-surface-low px-3 py-2 text-on-surface"
            />
          </label>
          {loi && <p className="text-sm text-negative">{loi}</p>}
          <button
            type="submit"
            disabled={dangLuu || !ngay}
            className="w-full rounded-xl bg-primary py-3 text-sm font-bold text-on-primary disabled:opacity-60"
          >
            {dangLuu ? "Đang lưu…" : "Lưu sự kiện"}
          </button>
        </form>
      </Card>
    </div>
  );
}
