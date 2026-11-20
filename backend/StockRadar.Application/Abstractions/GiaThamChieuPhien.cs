namespace StockRadar.Application.Abstractions;

/// <summary>Phiên mới nhất đã lưu của một mã và giá đóng cửa phiên liền trước (nghìn đồng, giá chưa điều chỉnh quyền).</summary>
public sealed record GiaThamChieuPhien(DateOnly NgayPhien, decimal GiaThamChieu);
