namespace StockRadar.Application.Options;

public sealed class MasterAlertOptions
{
    public const string SectionName = "MasterAlerts";

    public bool Enabled { get; set; } = true;

    /// <summary>% tăng tối thiểu so giá mở cửa phiên (Open) cho Mua điểm 1 (nhánh breakout).</summary>
    public decimal BuyPoint1MinChangePercent { get; set; } = 3m;

    /// <summary>% tăng tối thiểu so Open cho Mua điểm 2; cũng là cận trên band BuyPoint1.</summary>
    public decimal BuyPoint2MinChangePercent { get; set; } = 6m;

    /// <summary>% khoảng cách tối đa tới MA10 hoặc MA20 để nhánh pullback (chỉ BuyPoint1).</summary>
    public decimal PullbackNearMaPercent { get; set; } = 1.5m;

    /// <summary>% tăng tối thiểu từ Open khi kích hoạt nhánh pullback MA.</summary>
    public decimal PullbackMinGainFromOpenPercent { get; set; } = 0.5m;

    /// <summary>Nhánh pullback yêu cầu uptrend dài hạn (Close&gt;MA50, MA20≥MA50, slope MA20≥0).</summary>
    public bool PullbackRequireUptrendLong { get; set; } = true;

    /// <summary>KL khớp tối thiểu (legacy — không dùng cho Master alerts paced volume).</summary>
    public long MinSessionVolume { get; set; } = 800_000;

    /// <summary>Projected volume ratio tối thiểu cho Mua 1 nửa (so TB 20 phiên, điều chỉnh theo giờ).</summary>
    public decimal MinVolumeRatioPaced { get; set; } = 1.5m;

    /// <summary>Volume ratio tối thiểu riêng cho Mua hết (BuyPoint2).</summary>
    public decimal BuyPoint2MinVolumeRatio { get; set; } = 1.8m;

    /// <summary>Số chu kỳ quét liên tiếp giá giữ trên ngưỡng breakout trước khi bắn (~30–60s/chu kỳ).</summary>
    public int RequiredConfirmationTicks { get; set; } = 3;

    /// <summary>Sàn % phiên đã trôi khi tính paced volume (chống khuếch đại ATO đầu phiên). 0.2 ≈ 20% phiên.</summary>
    public decimal MinElapsedFractionForPacing { get; set; } = 0.2m;

    /// <summary>KL tuyệt đối tối thiểu (floor bảo vệ mã siêu nhỏ). 0 = tắt.</summary>
    public long MinSessionVolumeFloor { get; set; } = 50_000;

    /// <summary>Lợi nhuận đỉnh từ giá mua điểm 1 để Cắt lỗ điểm 1 (nhánh phân phối).</summary>
    public decimal CutLoss1MinPeakGainPercent { get; set; } = 4m;

    /// <summary>Lợi nhuận đỉnh từ giá mua điểm 1 để Cắt hết (nhánh phân phối).</summary>
    public decimal CutAllMinPeakGainPercent { get; set; } = 6.5m;

    /// <summary>Số phiên giao dịch tối thiểu kể từ ngày mua để mở cửa sổ BÁN (T+2.5 → mở sáng T+3).</summary>
    public int MinTradingSessionsToSell { get; set; } = 3;

    /// <summary>% sụt từ đỉnh (kể từ mua) để phát CẢNH BÁO rủi ro T+0 (chưa tới cửa sổ bán).</summary>
    public decimal RiskWarningDrawdownFromPeakPercent { get; set; } = 4m;

    /// <summary>% giảm so mốc tham chiếu để Bán 1 nửa (nhân hệ số pha).</summary>
    public decimal SellPoint1DropFromAnchorPercent { get; set; } = 4m;

    /// <summary>% giảm so mốc tham chiếu để Bán hết (nhân hệ số pha).</summary>
    public decimal SellPoint2DropFromAnchorPercent { get; set; } = 6m;

    /// <summary>Cửa sổ dựng mốc tham chiếu; mốc không lùi xa hơn ngày mở vị thế.</summary>
    public int AnchorLookbackSessions { get; set; } = 20;

    /// <summary>Độ dài tối thiểu của nền dùng làm vùng cản phía trên.</summary>
    public int OverheadBoxMinSessions { get; set; } = 20;

    /// <summary>Biên độ tối đa của nền vùng cản — tách khỏi <c>BreakoutMaxBoxHeightPercent</c> của nhận diện phá vỡ.</summary>
    public decimal OverheadBoxMaxHeightPercent { get; set; } = 15m;

    /// <summary>Nền kết thúc cách hiện tại quá số phiên này thì hết hiệu lực làm cản.</summary>
    public int OverheadBaseMaxAgeSessions { get; set; } = 250;

    /// <summary>% đệm chốt trước cạnh dưới nền (chia hệ số pha: chợ xấu lùi xa cản hơn).</summary>
    public decimal OverheadBaseBufferPercent { get; set; } = 0.5m;

    /// <summary>Số chu kỳ quét liên tiếp giá giữ qua ngưỡng trước khi bắn cảnh báo bán.</summary>
    public int SellConfirmationTicks { get; set; } = 2;

    /// <summary>Hệ số độ chặt theo pha: chợ xấu bán sớm (&lt;1), chợ tốt giữ lâu (&gt;1).</summary>
    public Dictionary<string, decimal> MarketPhaseMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Favorable"] = 1.25m,
        ["Neutral"] = 1.0m,
        ["Unfavorable"] = 0.75m,
    };

    public int CooldownMinutes { get; set; } = 15;

    /// <summary>% trượt giá tối đa cho phép khi đặt lệnh đuổi — hiển thị trong Telegram buy alerts.</summary>
    public decimal SlippageBufferPercent { get; set; } = 1.5m;

    /// <summary>Bật cổng ML P(hit) trước khi bắn BuyPoint (fail-open nếu model/feature thiếu).</summary>
    public bool MlGateEnabled { get; set; } = true;

    /// <summary>P(hit) tối thiểu (0–100) theo pha TT để bắn noti. Chỉ áp dụng khi model active + featuresComplete.</summary>
    public Dictionary<string, decimal> MinMlProbToFire { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Favorable"] = 45m,
        ["Neutral"] = 52m,
        ["Unfavorable"] = 60m,
    };

    // --- Phase 3: intraday orderflow model ---

    public bool IntradayModelEnabled { get; set; } = true;

    public string IntradayModelPath { get; set; } = "Data/vip-intraday-ranker.json";

    /// <summary>Khi cả daily + intraday active: dùng min(P) (thận trọng).</summary>
    public bool IntradayEnsembleWithDaily { get; set; } = true;

    public int IntradayMinSamplesToTrain { get; set; } = 30;

    public int IntradayTrainingEpochs { get; set; } = 800;

    public int IntradayDefaultDatasetDays { get; set; } = 90;

    // --- Phase 4: calibration + dynamic threshold + anti-spam ---

    public bool IntradayCalibrationEnabled { get; set; } = true;

    public string IntradayCalibrationPath { get; set; } = "Data/vip-intraday-calibration.json";

    public bool DynamicThresholdEnabled { get; set; } = true;

    public int DynamicThresholdLookbackDays { get; set; } = 20;

    /// <summary>Hit-rate intraday dưới sàn này → tăng MinMlProb (+DynamicThresholdBump).</summary>
    public decimal DynamicHitRateFloorPercent { get; set; } = 45m;

    public decimal DynamicThresholdBump { get; set; } = 5m;

    /// <summary>Khi P(hit) cách ngưỡng ≤ band → yêu cầu orderflow cùng chiều.</summary>
    public decimal AntiSpamBorderBandPercent { get; set; } = 5m;

    public bool AntiSpamRequireNonNegativeForeign { get; set; } = true;

    public bool AntiSpamBlockVsaXa { get; set; } = true;
}

public sealed class OpportunityPerformanceOptions
{
    public const string SectionName = "OpportunityPerformance";

    public bool Enabled { get; set; } = true;

    /// <summary>Số phiên chờ trước khi đo T+2.5.</summary>
    public int ForwardSessions { get; set; } = 2;

    /// <summary>ForwardSessions + 0.5 (T+2.5 VN).</summary>
    public int MinSessionsBeforeMeasure { get; set; } = 3;

    /// <summary>Win khi lãi T+2.5 ≥ ngưỡng (mặc định 1% — cover thuế/phí bán).</summary>
    public decimal SuccessThresholdPercent { get; set; } = 1m;

    /// <summary>Flat khi return ∈ [FlatMin, Success). Dưới FlatMin = Lose. Mặc định 0%.</summary>
    public decimal FlatMinPercent { get; set; } = 0m;

    /// <summary>Tỷ lệ hỏng vượt ngưỡng → đề xuất xem lại bộ lọc.</summary>
    public decimal MaxFailedRatePercent { get; set; } = 45m;

    public int WeeklyReviewHour { get; set; } = 15;

    public int WeeklyReviewMinute { get; set; } = 30;

    /// <summary>Thứ 6 — review tuần.</summary>
    public DayOfWeek WeeklyReviewDay { get; set; } = DayOfWeek.Friday;
}
