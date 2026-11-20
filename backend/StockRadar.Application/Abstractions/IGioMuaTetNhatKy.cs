using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>Đọc giỏ Mùa Tết đã mua từ Nhật ký lệnh của một tài khoản (spec 020 đợt 2c).</summary>
public interface IGioMuaTetNhatKy
{
    /// <summary>Mã đang giữ có lệnh Mua nguồn MUATET đúng mã mùa, kèm giá vốn bình quân và khối lượng còn giữ.</summary>
    /// <param name="userId">Tài khoản sở hữu giỏ.</param>
    /// <param name="maMua">Mã mùa, ví dụ 2026-27.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Rỗng khi tài khoản chưa có lệnh Mua MUATET của mùa.</returns>
    Task<IReadOnlyList<MuaTetMaNhatKy>> LayGioMuaTetAsync(Guid userId, string maMua, CancellationToken ct = default);
}
