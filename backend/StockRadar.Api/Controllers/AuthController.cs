using Microsoft.AspNetCore.Mvc;
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
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> CreateToken(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
            return Unauthorized();

        return Ok(new AuthResponseDto(result.UserId, result.Email, result.DisplayName, result.Token));
    }
}
