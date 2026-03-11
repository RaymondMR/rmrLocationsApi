using WebAppTest1.DTOs;
using WebAppTest1.Models;

namespace WebAppTest1.Services
{
    public interface IAuthService
    {
        Task<User?> RegisterAsync(UserDto request);
        Task<TokenResponseDto?> LoginAsync(UserDto request);
        Task<TokenResponseDto?> RefreshTokensAsync(RefreshTokenRequestDto request);
        Task LogoutAsync(Guid userId);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request);
    }
}
