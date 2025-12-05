using EShop.Application.Auth.Dtos;

namespace EShop.Application.Auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto input);
        Task<AuthResponseDto> LoginAsync(LoginDto input);
        Task<AuthResponseDto> LoginByOpenIdAsync(ExternalLoginDto input);
    }
}