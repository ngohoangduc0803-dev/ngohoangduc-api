
using System.ComponentModel.DataAnnotations;

namespace ngohoangduc.Models;

public class Employee
{
    public int Id { get; set; }

    [Required]
    [StringLength(30)]
    public string EmployeeCode { get; set; } = "";

    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [StringLength(100)]
    public string Department { get; set; } = "";

    [StringLength(100)]
    public string Position { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;
}
