namespace StockRadar.Application.DTOs;

public sealed record BanGhiSuKienQuyenDto(
    string Symbol,
    DateOnly ExDate,
    decimal Cash,
    decimal Dilution,
    int OldShares = 0,
    int NewShares = 0,
    decimal IssuePrice = 0);

public sealed record ThemSuKienQuyenRequest(
    DateOnly ExDate,
    decimal Cash = 0,
    decimal Dilution = 1m,
    int OldShares = 0,
    int NewShares = 0,
    decimal IssuePrice = 0);
