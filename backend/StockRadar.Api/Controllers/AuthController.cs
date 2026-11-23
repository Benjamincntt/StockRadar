using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[Tags("Auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("tokens")]
    [AllowAnonymous]
    [EnableRateLimiting("dang-nhap")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> CreateToken(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        // SECURITY: policy "dang-nhap" giới hạn 5 lần/60 giây mỗi IP; gỡ attribute này là dò được mật khẩu vô hạn.
        var result = await auth.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
            return Unauthorized();

        return Ok(new AuthResponseDto(result.UserId, result.Email, result.DisplayName, result.Token));
    }
}
