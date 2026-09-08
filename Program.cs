using ECommerceSite.Data;
using ECommerceSite.Models;
using ECommerceSite.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed. Verify the PostgreSQL connection string and database availability.");
        throw;
    }

    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    if (!await roleManager.RoleExistsAsync("Customer"))
        await roleManager.CreateAsync(new IdentityRole("Customer"));

    var adminEmail = (builder.Configuration["Seed:AdminEmail"] ?? "admin@yourdomain.com").Trim();
    var adminPassword = (builder.Configuration["Seed:AdminPassword"] ?? "AdminPass123!").Trim();

    if (string.IsNullOrWhiteSpace(adminEmail))
    {
        app.Logger.LogWarning("Admin seed email is empty. Skipping admin seeding.");
    }
    else
    {
        var adminUser = await userManager.FindByEmailAsync(adminEmail) ?? await userManager.FindByNameAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Admin",
                EmailConfirmed = true
            };

            var seedResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!seedResult.Succeeded)
            {
                foreach (var error in seedResult.Errors)
                {
                    app.Logger.LogError("Admin seeding failed: {Code} - {Description}", error.Code, error.Description);
                }
            }
            else
            {
                app.Logger.LogWarning(
                    "Seeded default admin account ({Email}). Configure Seed:AdminEmail / Seed:AdminPassword in environment variables or user secrets before production.", adminEmail);
            }
        }

        var seededAdmin = await userManager.FindByEmailAsync(adminEmail) ?? await userManager.FindByNameAsync(adminEmail);
        if (seededAdmin != null && !await userManager.IsInRoleAsync(seededAdmin, "Admin"))
        {
            await userManager.AddToRoleAsync(seededAdmin, "Admin");
        }
    }

    if (!await context.BusinessSettings.AnyAsync())
    {
        context.BusinessSettings.Add(new BusinessSettings
        {
            BusinessName = builder.Configuration["Business:Name"] ?? "Local Pork Delivery",
            Phone = builder.Configuration["Business:Phone"] ?? "+91-XXXXXXXXXX",
            WhatsApp = builder.Configuration["Business:WhatsApp"] ?? "91XXXXXXXXXX",
            ServiceArea = builder.Configuration["Business:ServiceArea"] ?? "Local delivery within service area",
            Address = builder.Configuration["Business:Address"] ?? "Service area only",
            BusinessHours = builder.Configuration["Business:Hours"] ?? "Mon-Sun, 9AM-8PM",
            MinimumOrderEnabled = true,
            MinimumOrderValue = 500m,
            CashOnDeliveryEnabled = true
        });
        await context.SaveChangesAsync();
    }
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();
