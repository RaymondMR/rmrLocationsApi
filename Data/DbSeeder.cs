using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebAppTest1.Models;

namespace WebAppTest1.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdminUser(AppDbContext context)
        {
            // Check if admin already exists
            if (await context.Users.AnyAsync(u => u.Username == "admin"))
            {
                return;
            }

            var admin = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Roles = [UserRoles.Admin, UserRoles.User]
            };

            // Securely hash the default password
            admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, "Admin123!");

            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }
    }
}
