using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using WebAppTest1.DTOs;
using WebAppTest1.Models;
using WebAppTest1.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace WebAppTest1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthService authService) : ControllerBase
    {

        [HttpPost("register")]
        public async Task<ActionResult<UserResponseDto>> Register(UserDto request)
        {
            var user = await authService.RegisterAsync(request);
            if (user is null)
                return BadRequest("Username already exists.");

            var response = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Roles = user.Roles
            };

            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<ActionResult<TokenResponseDto>> Login(UserDto request)
        {
            var result = await authService.LoginAsync(request);
            if (result is null)
                return BadRequest("Invalid username or password.");

            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null)
            {
                return Unauthorized();
            }

            var userId = Guid.Parse(userIdString);
            await authService.LogoutAsync(userId);
            return Ok("Logged out successfully.");
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();

            var userId = Guid.Parse(userIdString);
            var result = await authService.ChangePasswordAsync(userId, request);

            if (!result) return BadRequest("Invalid current password or user not found.");

            return Ok("Password updated successfully. Please log in again.");
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<TokenResponseDto>> RefreshToken(RefreshTokenRequestDto request)
        {
            var result = await authService.RefreshTokensAsync(request);
            if (result is null || result.AccessToken is null || result.RefreshToken is null)
                return Unauthorized("Invalid refresh token.");

            return Ok(result);
        }

        [Authorize]
        [HttpGet]
        public IActionResult AuthenticatedOnlyEndpoint()
        {
            return Ok("You are authenticated!");
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpGet("admin-only")]
        public IActionResult AdminOnlyEndpoint()
        {
            return Ok("You are and admin!");
        }
    }
}
