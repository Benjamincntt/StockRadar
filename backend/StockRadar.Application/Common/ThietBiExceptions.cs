namespace StockRadar.Application.Common;

public sealed class ThietBiValidationException(string detail) : Exception(detail);
