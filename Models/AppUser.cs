
using Microsoft.AspNetCore.Identity;

namespace ngohoangduc.Models;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
