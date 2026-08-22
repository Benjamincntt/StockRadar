namespace StockRadar.Application.DTOs;

public sealed record BanGhiSuKienQuyenDto(
    string Symbol,
    DateOnly ExDate,
    decimal Cash,
    decimal Dilution);

public sealed record ThemSuKienQuyenRequest(
    DateOnly ExDate,
    decimal Cash = 0,
    decimal Dilution = 1m);
