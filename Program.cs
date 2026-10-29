
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ngohoangduc.Models;
using ngohoangduc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// SQL SERVER
var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection"
    ) ?? throw new InvalidOperationException(
        "Chua cau hinh DefaultConnection"
    );

builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString)
);

// IDENTITY
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<EmailService>();

// JWT
var secret = builder.Configuration["Jwt:Secret"];
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(secret) ||
    Encoding.UTF8.GetByteCount(secret) < 32 ||
    string.IsNullOrWhiteSpace(issuer) ||
    string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException(
        "Chua cau hinh JWT Secret, Issuer hoac Audience"
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
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secret)
                    ),
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// TAO ROLE VA CAP ADMIN MOT LAN
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var role in new[] { "Admin", "Employee" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var created = await roleManager.CreateAsync(
                new IdentityRole(role)
            );

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Khong tao duoc role " + role
                );
            }
        }
    }

    // Chi cho phep cap Admin trong Development,
    // khi quan tri vien chu dong dat bien moi truong.
    var adminEmail = Environment.GetEnvironmentVariable(
        "ADMIN_BOOTSTRAP_EMAIL"
    );

    if (app.Environment.IsDevelopment() &&
        !string.IsNullOrWhiteSpace(adminEmail))
    {
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByEmailAsync(
            adminEmail.Trim()
        );

        if (user == null)
        {
            throw new InvalidOperationException(
                "Khong tim thay tai khoan Admin. "
                + "Hay dang ky truoc."
            );
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            var result = await userManager.AddToRoleAsync(
                user, "Admin"
            );

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Khong the cap quyen Admin: "
                    + string.Join("; ", result.Errors.Select(
                        e => e.Description
                    ))
                );
            }
        }

        Console.WriteLine(
            "CAP QUYEN ADMIN THANH CONG: " + user.Email
        );
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    application = "ngohoangduc",
    group = 7
}));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
