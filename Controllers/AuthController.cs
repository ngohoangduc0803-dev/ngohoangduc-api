
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

using ngohoangduc.Models;
using ngohoangduc.Services;

namespace ngohoangduc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly EmailService _email;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;

    public AuthController(
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        EmailService email,
        IConfiguration config,
        IMemoryCache cache)
    {
        _users = users;
        _signIn = signIn;
        _email = email;
        _config = config;
        _cache = cache;
    }

    // 1. DANG KY TAI KHOAN NHAN VIEN
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Vui long nhap day du thong tin"
            });
        }

        var existing = await _users.FindByEmailAsync(
            request.Email.Trim()
        );

        if (existing != null)
        {
            return Conflict(new
            {
                message = "Email da duoc dang ky"
            });
        }

        var user = new AppUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            EmployeeCode = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _users.CreateAsync(
            user,
            request.Password
        );

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(
                    e => e.Description
                )
            });
        }

        // Dang ky cong khai chi duoc cap Employee.
        // Khong cho nguoi dung tu dang ky Admin.
        var roleResult = await _users.AddToRoleAsync(
            user,
            "Employee"
        );

        if (!roleResult.Succeeded)
        {
            return StatusCode(500, new
            {
                message =
                    "Da tao tai khoan nhung chua gan duoc role"
            });
        }

        return StatusCode(201, new
        {
            message = "Dang ky thanh cong",
            email = user.Email,
            role = "Employee"
        });
    }

    // 2. KIEM TRA MAT KHAU VA GUI OTP
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Thieu email hoac mat khau"
            });
        }

        var user = await _users.FindByEmailAsync(
            request.Email.Trim()
        );

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Email hoac mat khau khong dung"
            });
        }

        var check = await _signIn.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true
        );

        if (!check.Succeeded)
        {
            return Unauthorized(new
            {
                message =
                    "Thong tin dang nhap khong hop le"
            });
        }

        var cacheKey = $"otp-send:{user.Id}";

        if (_cache.TryGetValue(cacheKey, out _))
        {
            return StatusCode(429, new
            {
                message =
                    "Vui long doi 60 giay truoc khi yeu cau OTP"
            });
        }

        var otp = await _users.GenerateTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultEmailProvider
        );

        try
        {
            await _email.SendOtpAsync(
                user.Email!,
                otp
            );
        }
        catch
        {
            return StatusCode(503, new
            {
                message =
                    "Khong gui duoc email OTP. "
                    + "Vui long kiem tra cau hinh SMTP"
            });
        }

        _cache.Set(
            cacheKey,
            true,
            TimeSpan.FromSeconds(60)
        );

        return Ok(new
        {
            message = "Da gui OTP den email",
            requiresOtp = true,
            email = user.Email
        });
    }

    // 3. XAC MINH OTP VA CAP JWT
    [AllowAnonymous]
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request)
    {
        var user = await _users.FindByEmailAsync(
            request.Email?.Trim() ?? ""
        );

        if (user == null ||
            string.IsNullOrWhiteSpace(request.Otp))
        {
            return Unauthorized(new
            {
                message = "OTP khong hop le"
            });
        }

        var attemptsKey = $"otp-attempts:{user.Id}";

        _cache.TryGetValue(
            attemptsKey,
            out int attempts
        );

        if (attempts >= 5)
        {
            return StatusCode(429, new
            {
                message =
                    "Qua nhieu lan thu OTP. "
                    + "Vui long thu lai sau"
            });
        }

        var valid = await _users.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultEmailProvider,
            request.Otp.Trim()
        );

        if (!valid)
        {
            _cache.Set(
                attemptsKey,
                attempts + 1,
                TimeSpan.FromMinutes(10)
            );

            return Unauthorized(new
            {
                message = "OTP sai hoac da het han"
            });
        }

        _cache.Remove(attemptsKey);

        var roles = await _users.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id
            ),
            new(
                ClaimTypes.Name,
                user.UserName ?? ""
            ),
            new(
                ClaimTypes.Email,
                user.Email ?? ""
            ),
            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()
            )
        };

        foreach (var role in roles)
        {
            claims.Add(
                new Claim(ClaimTypes.Role, role)
            );
        }

        var secret = _config["Jwt:Secret"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret)
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var expires = DateTime.UtcNow.AddHours(2);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        var token = new JwtSecurityTokenHandler()
            .WriteToken(jwt);

        return Ok(new
        {
            message = "Dang nhap thanh cong",
            token,
            email = user.Email,
            fullName = user.FullName,
            roles,
            expiresAt = expires
        });
    }

    // 4. LAY THONG TIN NGUOI DUNG
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (string.IsNullOrWhiteSpace(id))
        {
            return Unauthorized();
        }

        var user = await _users.FindByIdAsync(id);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _users.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.EmployeeCode,
            roles
        });
    }
}

public class RegisterRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class VerifyOtpRequest
{
    public string Email { get; set; } = "";
    public string Otp { get; set; } = "";
}
