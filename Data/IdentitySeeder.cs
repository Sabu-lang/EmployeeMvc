using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EmployeeMvc.Models;

namespace EmployeeMvc.Data
{






    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

            try
            {
                var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
                var context = sp.GetRequiredService<ApplicationDbContext>();
                var seed = sp.GetRequiredService<IOptions<SeedAdminSettings>>().Value;

                foreach (var role in AppRoles.All)
                {
                    if (await roleManager.RoleExistsAsync(role)) continue;

                    var created = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!created.Succeeded)
                    {
                        logger.LogError("როლის '{Role}' შექმნა ვერ მოხერხდა: {Errors}", role,
                            string.Join("; ", created.Errors.Select(e => e.Description)));
                    }
                }

                if (!string.IsNullOrWhiteSpace(seed.Email))
                {
                    var admin = await userManager.FindByEmailAsync(seed.Email);

                    if (admin == null)
                    {
                        if (string.IsNullOrWhiteSpace(seed.Password))
                        {
                            logger.LogWarning("SeedAdmin:Email მითითებულია, მაგრამ SeedAdmin:Password ცარიელია — Admin ვერ შეიქმნა.");
                        }
                        else
                        {
                            admin = new IdentityUser { UserName = seed.Email, Email = seed.Email, EmailConfirmed = true };
                            var created = await userManager.CreateAsync(admin, seed.Password);
                            if (!created.Succeeded)
                            {
                                logger.LogError("Admin-ის შექმნა ვერ მოხერხდა: {Errors}",
                                    string.Join("; ", created.Errors.Select(e => e.Description)));
                                admin = null;
                            }
                        }
                    }
                    else if (!admin.EmailConfirmed)
                    {

                        logger.LogWarning("მომხმარებელი {Email} არსებობს, მაგრამ ელფოსტა დადასტურებული არ არის — Admin როლი არ მიენიჭა.", seed.Email);
                        admin = null;
                    }

                    if (admin != null && !await userManager.IsInRoleAsync(admin, AppRoles.Admin))
                    {
                        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
                        logger.LogInformation("მომხმარებელს {Email} მიენიჭა Admin როლი.", seed.Email);
                    }
                }

                var withoutRole = await context.Users
                    .Where(u => !context.UserRoles.Any(ur => ur.UserId == u.Id))
                    .ToListAsync();

                foreach (var user in withoutRole)
                {
                    await userManager.AddToRoleAsync(user, AppRoles.Employee);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Identity seeding ვერ შესრულდა. შეამოწმეთ, რომ მიგრაციები გატარებულია (dotnet ef database update).");
            }
        }
    }
}
