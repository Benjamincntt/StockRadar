namespace StockRadar.Application.Common;

public sealed class AppException : Exception
{
    public AppException(string title, string message, int statusCode)
        : base(message)
    {
        Title = title;
        StatusCode = statusCode;
    }

    public string Title { get; }
    public int StatusCode { get; }
}
