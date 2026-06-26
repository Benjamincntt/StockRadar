namespace StockRadar.Application.DTOs;

public record RegisterRequest(string Email, string Password, string DisplayName);

public record LoginRequest(string Email, string Password);

public record AuthResponseDto(Guid UserId, string Email, string DisplayName, string Token);
