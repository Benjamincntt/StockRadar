# Phase 1 — Contracts: quyền mua trả tiền

Mở rộng C-1/C-2 spec 005. Route `GET/POST /api/v1/stocks/{symbol}/rights-events` **additive**.

## C-1. File seed `su-kien-quyen.json`

```json
{
  "suKien": [
    {
      "ma": "SSI",
      "ngayKhongHuongQuyen": "2026-08-17",
      "tienMat": 1.0,
      "heSoPhaLoang": 1.2
    },
    {
      "ma": "HCM",
      "ngayKhongHuongQuyen": "2026-02-05",
      "tienMat": 0.4,
      "heSoPhaLoang": 1
    },
    {
      "ma": "HCM",
      "ngayKhongHuongQuyen": "2026-07-16",
      "tienMat": 0.4,
      "heSoPhaLoang": 1,
      "soCoCu": 4,
      "soCoMoi": 1,
      "giaPhatHanh": 10.0
    }
  ]
}
```

Thiếu `soCoCu`/`soCoMoi`/`giaPhatHanh` → 0 (SSI cũ vẫn nạp).

## C-2. API JSON (English keys đã ship)

`GET/POST` body/item thêm optional: `oldShares`, `newShares`, `issuePrice` (mặc định 0). Giữ `symbol`, `exDate`, `cash`, `dilution`.

POST từ chối: `newShares` > 0 và `oldShares` ≤ 0; `issuePrice` < 0; `cash` ≥ 100 (như hiện tại).

## C-3. `TinhGiaThamChieu`

Tham số thêm `tyLeQuyenMua`, `giaPhatHanh` mặc định 0. Caller SSI không đổi chữ ký 3 đối số.
