using rmrLocationsApi.Data;
using rmrLocationsApi.DTOs;
using rmrLocationsApi.Models;
using rmrLocationsApi.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace rmrLocationsApi.Services;

public class AuthService(
    AppDbContext context,
    IOptions<JwtSettings> jwtSettings) : IAuthService
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public async Task<TokenResponseDto?> LoginAsync(UserDto request)
    {
        // Solo lectura: el refresh token se escribe con un UPDATE atómico (StoreRefreshTokenAsync),
        // sin pasar por el change tracker (ver el comentario de RefreshTokensAsync).
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == request.UserName);
        if (user is null || new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // Un login nuevo reemplaza la sesión anterior (un solo refresh token por usuario): gana el último.
        var refreshToken = GenerateRefreshToken();
        await StoreRefreshTokenAsync(user.Id, refreshToken, replacing: null);

        return new TokenResponseDto { AccessToken = CreateAccessToken(user), RefreshToken = refreshToken };
    }

    public async Task<User?> RegisterAsync(UserDto request)
    {
        if (await context.Users.AnyAsync(u => u.UserName == request.UserName || u.Email == request.Email))
        {
            return null;
        }

        var user = new User
        {
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = new PasswordHasher<User>().HashPassword(new User(), request.Password),
            Roles = [UserRoles.User]
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<TokenResponseDto?> RefreshTokensAsync(RefreshTokenRequestDto request)
    {
        var user = await ValidateRefreshTokenAsync(request.UserId, request.RefreshToken);
        if (user is null)
        {
            return null;
        }

        // Rotación atómica (compare-and-swap): el UPDATE solo afecta la fila si el token guardado sigue siendo
        // el presentado. Si dos refresh llegan a la vez con el mismo token, solo uno rota (1 fila); el otro
        // pierde la carrera (0 filas) y recibe null -> 401. Con el change tracker, el segundo SaveChanges
        // chocaba con el token de concurrencia xmin y terminaba en DbUpdateConcurrencyException (500).
        var newRefreshToken = GenerateRefreshToken();
        var rotated = await StoreRefreshTokenAsync(user.Id, newRefreshToken, replacing: request.RefreshToken);
        if (rotated == 0)
        {
            return null;
        }

        return new TokenResponseDto { AccessToken = CreateAccessToken(user), RefreshToken = newRefreshToken };
    }

    public async Task LogoutAsync(Guid userId)
    {
        // UPDATE directo: idempotente y sin conflicto de concurrencia con un refresh/login simultáneo.
        await context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.RefreshToken, (string?)null)
                .SetProperty(u => u.RefreshTokenExpiryTime, (DateTime?)null)
                .SetProperty(u => u.UpdatedAtUtc, (DateTimeOffset?)DateTimeOffset.UtcNow));
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request)
    {
        var user = await context.Users.FindAsync(userId);
        if (user is null || new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.NewPassword);
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await context.SaveChangesAsync();
        return true;
    }

    private string CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Token));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Name, user.UserName),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Guarda un refresh token nuevo con un UPDATE atómico (sin change tracker ni token de concurrencia xmin).
    /// Con <paramref name="replacing"/> es compare-and-swap: solo escribe si el token guardado sigue siendo ese
    /// y devuelve las filas afectadas (0 = otro request ya lo rotó o lo revocó). Con null escribe siempre
    /// (login: gana el último).
    /// </summary>
    private async Task<int> StoreRefreshTokenAsync(Guid userId, string newRefreshToken, string? replacing)
    {
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var users = context.Users.Where(u => u.Id == userId);
        if (replacing is not null)
        {
            users = users.Where(u => u.RefreshToken == replacing);
        }

        return await users.ExecuteUpdateAsync(s => s
            .SetProperty(u => u.RefreshToken, newRefreshToken)
            .SetProperty(u => u.RefreshTokenExpiryTime, (DateTime?)expiresAt)
            .SetProperty(u => u.UpdatedAtUtc, (DateTimeOffset?)DateTimeOffset.UtcNow));
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private async Task<User?> ValidateRefreshTokenAsync(Guid userId, string refreshToken)
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        // Un usuario sin sesión (logout / cambio de contraseña) tiene token y vencimiento en null: nunca
        // debe validar, ni siquiera con un refreshToken null en el request.
        if (user?.RefreshToken is null
            || user.RefreshToken != refreshToken
            || user.RefreshTokenExpiryTime is null
            || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return null;
        }

        return user;
    }
}
