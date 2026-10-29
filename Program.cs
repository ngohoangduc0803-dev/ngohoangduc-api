
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using ngohoangduc.Models;
using ngohoangduc.Services;

var builder = WebApplication.CreateBuilder(args);

// ==============================================
// 1. SERVICES
// ==============================================

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// ==============================================
// 2. DATABASE: SQL SERVER / NEON POSTGRESQL
// ==============================================

var databaseProvider =
    builder.Configuration["DatabaseProvider"] ?? "SqlServer";

if (databaseProvider.Equals(
    "PostgreSQL",
    StringComparison.OrdinalIgnoreCase))
{
    var neonConnection =
        builder.Configuration.GetConnectionString("NeonConnection")
        ?? throw new InvalidOperationException(
            "Chua cau hinh ConnectionStrings:NeonConnection"
        );

    // Neon cung cap URL dang postgresql://...
    // Npgsql su dung connection string dang Host=...
    if (neonConnection.StartsWith(
        "postgresql://",
        StringComparison.OrdinalIgnoreCase) ||
        neonConnection.StartsWith(
        "postgres://",
        StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(neonConnection);

        var credentials = uri.UserInfo.Split(':', 2);

        if (credentials.Length != 2)
        {
            throw new InvalidOperationException(
                "Neon URL thieu username hoac password"
            );
        }

        var user = Uri.UnescapeDataString(credentials[0]);
        var password = Uri.UnescapeDataString(credentials[1]);

        var database = Uri.UnescapeDataString(
            uri.AbsolutePath.TrimStart('/')
        );

        var connectionBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = database,
            Username = user,
            Password = password,
            SslMode = SslMode.Require
        };

        neonConnection = connectionBuilder.ConnectionString;
    }

    builder.Services.AddDbContext<AppDbContext>(
        options => options.UseNpgsql(neonConnection)
    );

    Console.WriteLine(
        "DATABASE PROVIDER: NEON POSTGRESQL"
    );
}
else if (databaseProvider.Equals(
    "SqlServer",
    StringComparison.OrdinalIgnoreCase))
{
    var sqlConnection =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Chua cau hinh ConnectionStrings:DefaultConnection"
        );

    builder.Services.AddDbContext<AppDbContext>(
        options => options.UseSqlServer(sqlConnection)
    );

    Console.WriteLine(
        "DATABASE PROVIDER: SQL SERVER"
    );
}
else
{
    throw new InvalidOperationException(
        "DatabaseProvider phai la SqlServer hoac PostgreSQL"
    );
}

// ==============================================
// 3. ASP.NET CORE IDENTITY
// ==============================================

builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ==============================================
// 4. JWT AUTHENTICATION
// ==============================================

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "Chua cau hinh Jwt:Secret"
    );

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Chua cau hinh Jwt:Issuer"
    );

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Chua cau hinh Jwt:Audience"
    );

if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret phai dai it nhat 32 bytes"
    );
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret)
                    ),

                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role,

                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization();

// ==============================================
// 5. EMAIL OTP SERVICE
// ==============================================

builder.Services.AddScoped<EmailService>();

// ==============================================
// 6. CREATE APPLICATION
// ==============================================

var app = builder.Build();

// ==============================================
// 7. HEALTH CHECK
// ==============================================

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        application = "ngohoangduc",
        group = 7
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ==============================================
// 8. MIDDLEWARE
// ==============================================

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ==============================================
// 9. SEED IDENTITY ROLES
// Chi chay sau khi database da co bang Identity
// ==============================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    if (await db.Database.CanConnectAsync())
    {
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { "Admin", "Employee" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole(role)
                );

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Khong tao duoc role " + role
                    );
                }
            }
        }

        // Chi cho phep cap Admin trong Development.
        if (app.Environment.IsDevelopment())
        {
            var adminEmail =
                Environment.GetEnvironmentVariable(
                    "ADMIN_BOOTSTRAP_EMAIL"
                );

            if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                var userManager = scope.ServiceProvider
                    .GetRequiredService<UserManager<AppUser>>();

                var user = await userManager.FindByEmailAsync(
                    adminEmail
                );

                if (user != null &&
                    !await userManager.IsInRoleAsync(user, "Admin"))
                {
                    var result = await userManager.AddToRoleAsync(
                        user,
                        "Admin"
                    );

                    if (result.Succeeded)
                    {
                        Console.WriteLine(
                            "CAP QUYEN ADMIN THANH CONG: " +
                            user.Email
                        );
                    }
                }
            }
        }
    }
    else
    {
        Console.WriteLine(
            "DATABASE CHUA KET NOI DUOC. " +
            "CAN KIEM TRA CONNECTION STRING."
        );
    }
}

// ==============================================
// 10. RUN API
// ==============================================

app.Run();
