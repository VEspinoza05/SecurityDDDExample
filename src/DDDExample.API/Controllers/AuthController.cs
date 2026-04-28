
using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DDDExample.API.Authentication.JwtMfa.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IRefreshTokenService _refreshTokenService;

    public AuthController(IAuthService authService, IRefreshTokenService refreshTokenService)
    {
        _authService = authService;
        _refreshTokenService = refreshTokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        
        if (result.RequiresMfa)
        {
            return Ok(new LoginResponse
            {
                RequiresMfa = true,
                MfaToken = result.MfaToken,
                User = result.User
            });
        }

        if (string.IsNullOrEmpty(result.Token))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        return Ok(result);
    }

    [HttpPost("verify-mfa")]
    public async Task<ActionResult<LoginResponse>> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        var result = await _authService.VerifyMfaAsync(request);
        
        if (string.IsNullOrEmpty(result.Token))
        {
            return BadRequest(new { message = "Invalid MFA code" });
        }

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _refreshTokenService.RotateRefreshTokenAsync(request.RefreshToken);
        
        if (string.IsNullOrEmpty(result))
        {
            return BadRequest(new { message = "Invalid refresh token" });
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _refreshTokenService.RevokeUserTokensAsync(Guid.Parse(userId));
        return Ok(new { message = "Logged out successfully" });
    }
}