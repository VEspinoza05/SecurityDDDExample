using DDDExample.Application.DTOs;
using DDDExample.Domain.Entities;

namespace DDDExample.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> VerifyMfaAsync(MfaVerifyRequest request);
}