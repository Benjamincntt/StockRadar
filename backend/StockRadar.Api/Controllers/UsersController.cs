using Microsoft.AspNetCore.Mvc;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Tags("Users")]
public sealed class UsersController(IAuthService auth) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Create(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            cancellationToken);

        var response = new AuthResponseDto(result.UserId, result.Email, result.DisplayName, result.Token);
        return Created($"/api/v1/users/{result.UserId}", response);
    }
}
