using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using DDDExample.Infrastructure.Persistence.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace DDDExample.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ApplicationDbContext _context;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _context = context;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return new LoginResponse { RequiresMfa = false };
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Si MFA está habilitado, requerir segundo factor
        if (user.MfaEnabled)
        {
            var mfaToken = GenerateMfaToken(user);
            return new LoginResponse
            {
                RequiresMfa = true,
                MfaToken = mfaToken,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FullName = user.FullName,
                    CreatedAt = user.CreatedAt,
                    MfaEnabled = user.MfaEnabled
                }
            };
        }

        // Generar tokens si no hay MFA
        var jwtToken = _tokenService.GenerateToken(user);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);

        return new LoginResponse
        {
            Token = jwtToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                MfaEnabled = user.MfaEnabled
            }
        };
    }

    public async Task<LoginResponse> VerifyMfaAsync(MfaVerifyRequest request)
    {
        var userId = ValidateMfaToken(request.MfaToken);
        if (userId == null)
        {
            return new LoginResponse { RequiresMfa = false };
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.MfaEnabled)
        {
            return new LoginResponse { RequiresMfa = false };
        }

        // Verificar código TOTP o backup code
        bool isValid = false;
        if (request.Code.Length == 6)
        {
            var mfaService = new TotpMfaService(Options.Create(new MfaSettings()));
            isValid = mfaService.VerifyToken(user.MfaSecret!, request.Code);
        }
        else
        {
            isValid = VerifyBackupCode(user, request.Code);
        }

        if (!isValid)
        {
            return new LoginResponse { RequiresMfa = false };
        }

        // Generar tokens
        var jwtToken = _tokenService.GenerateToken(user);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);

        // Remember device si se solicita
        if (request.RememberDevice)
        {
            await RememberDeviceAsync(user.Id);
        }

        return new LoginResponse
        {
            Token = jwtToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                MfaEnabled = user.MfaEnabled
            }
        };
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = "Email already exists"
            };
        }

        var user = new ApplicationUser(request.Email, request.FirstName, request.LastName);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        return new RegisterResponse
        {
            Success = true,
            Message = "User registered successfully",
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt
            }
        };
    }

    private string GenerateMfaToken(ApplicationUser user)
    {
        // Generar token temporal para MFA (válido por 5 minutos)
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCIIBytes("temporary-mfa-secret-key-32-chars");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("mfa-challenge", "true")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private Guid? ValidateMfaToken(string mfaToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCIIBytes("temporary-mfa-secret-key-32-chars");
            tokenHandler.ValidateToken(mfaToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
            return Guid.Parse(userId);
        }
        catch
        {
            return null;
        }
    }

    private bool VerifyBackupCode(ApplicationUser user, string code)
    {
        if (string.IsNullOrEmpty(user.BackupCodes))
            return false;

        var codes = JsonSerializer.Deserialize<List<string>>(user.BackupCodes);
        if (codes == null || !codes.Contains(code))
            return false;

        // Remover código usado
        codes.Remove(code);
        user.BackupCodes = JsonSerializer.Serialize(codes);
        _userManager.UpdateAsync(user);

        return true;
    }

    private async Task RememberDeviceAsync(Guid userId)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceFingerprint = GenerateDeviceFingerprint(),
            RememberMfaUntil = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        };

        _context.UserSessions.Add(session);
        await _context.SaveChangesAsync();
    }

    private string GenerateDeviceFingerprint()
    {
        // Implementar lógica para generar fingerprint de dispositivo
        return Guid.NewGuid().ToString();
    }
}