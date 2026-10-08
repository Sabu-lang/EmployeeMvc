using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Authorization;
using EmployeeMvc.Data;
using EmployeeMvc.Models;
using EmployeeMvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// კონფიგურაცია: appsettings.json-ში მხოლოდ არასაიდუმლო მნიშვნელობები.
// საიდუმლოები — user-secrets (ლოკალურად) ან environment variables (მაგ. EmailSettings__SenderPassword).
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.Configure<SeedAdminSettings>(builder.Configuration.GetSection("SeedAdmin"));
builder.Services.AddHttpClient<IEmailSender, EmailSender>();

builder.Services.AddScoped<ICurrentEmployeeService, CurrentEmployeeService>();
builder.Services.AddSingleton<IAuthorizationHandler, GroupAuthorizationHandler>();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// როლის/პაროლის/2FA ცვლილება ძალაში შედის მომდევნო მოთხოვნიდანვე
// (security stamp მოწმდება ყოველ მოთხოვნაზე, ამიტომ ჩამოშორებული როლი cookie-ში არ რჩება).
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// Password reset ტოკენი მოქმედია 1 საათი.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{

    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// როლების (და სურვილისამებრ პირველი Admin-ის) seeding
await IdentitySeeder.SeedAsync(app.Services);

app.Run();
