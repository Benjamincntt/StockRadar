namespace StockRadar.Application.Options;

public sealed class SuKienQuyenOptions
{
    public const string SectionName = "SuKienQuyen";

    /// <summary>Đường dẫn tương đối content root API. Không chứa secret.</summary>
    public string FilePath { get; set; } = "Data/su-kien-quyen.json";
}
