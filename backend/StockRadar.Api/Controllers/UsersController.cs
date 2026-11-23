using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;

namespace StockRadar.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Tags("Users")]
public sealed class UsersController(IAuthService auth, IOptions<AuthOptions> authOptions) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Create(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        // SECURITY: đăng ký đóng mặc định; bỏ kiểm này thì ai cũng tạo được tài khoản và vào được mọi dữ liệu.
        if (!authOptions.Value.ChoPhepDangKy)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Đăng ký đã tắt",
                Status = StatusCodes.Status403Forbidden,
                Detail = "API này không mở đăng ký. Liên hệ quản trị để được cấp tài khoản."
            });

        var result = await auth.RegisterAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            cancellationToken);

        var response = new AuthResponseDto(result.UserId, result.Email, result.DisplayName, result.Token);
        return Created($"/api/v1/users/{result.UserId}", response);
    }
}
