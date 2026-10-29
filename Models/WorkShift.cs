
using System.ComponentModel.DataAnnotations;

namespace ngohoangduc.Models;

public class WorkShift
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    [StringLength(250)]
    public string Description { get; set; } = "";

    public bool IsActive { get; set; } = true;
}
