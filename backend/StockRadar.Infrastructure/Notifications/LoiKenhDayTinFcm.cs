using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Contains only allowlisted initialization diagnostics, never original exception details or an inner exception.</summary>
internal sealed class LoiKenhDayTinFcm : Exception
{
    internal string GiaiDoan => "khoi_tao";
    internal string LoaiLoi { get; }
    internal string KieuLoi { get; }
    internal MessagingErrorCode? MessagingErrorCode { get; }

    private LoiKenhDayTinFcm(string loaiLoi, string kieuLoi, MessagingErrorCode? maLoi)
        : base("FCM initialization unavailable.")
    {
        LoaiLoi = loaiLoi;
        KieuLoi = kieuLoi;
        MessagingErrorCode = maLoi;
    }

    internal LoiKenhDayTinFcm SaoChep() => new(LoaiLoi, KieuLoi, MessagingErrorCode);

    internal static LoiKenhDayTinFcm Tao(Exception loi)
    {
        var nhom = loi switch
        {
            TimeoutException or OperationCanceledException => "qua_han",
            HttpRequestException { StatusCode: HttpStatusCode.RequestTimeout } => "qua_han",
            HttpRequestException { StatusCode: { } maHttp } when (int)maHttp is >= 400 and < 500 => "xac_thuc",
            HttpRequestException or SocketException => "mang",
            FirebaseException { ErrorCode: ErrorCode.Unavailable } => "mang",
            _ => "xac_thuc"
        };
        var kieu = loi switch
        {
            TaskCanceledException => nameof(TaskCanceledException),
            OperationCanceledException => nameof(OperationCanceledException),
            TimeoutException => nameof(TimeoutException),
            HttpRequestException => nameof(HttpRequestException),
            SocketException => nameof(SocketException),
            FirebaseMessagingException => nameof(FirebaseMessagingException),
            FirebaseException => nameof(FirebaseException),
            JsonException => nameof(JsonException),
            FormatException => nameof(FormatException),
            UnauthorizedAccessException => nameof(UnauthorizedAccessException),
            FileNotFoundException => nameof(FileNotFoundException),
            IOException => nameof(IOException),
            ArgumentException => nameof(ArgumentException),
            InvalidOperationException => nameof(InvalidOperationException),
            _ => nameof(Exception)
        };
        var ma = loi is FirebaseMessagingException { MessagingErrorCode: { } giaTri } && Enum.IsDefined(giaTri)
            ? giaTri : (MessagingErrorCode?)null;
        return new LoiKenhDayTinFcm(nhom, kieu, ma);
    }
}
