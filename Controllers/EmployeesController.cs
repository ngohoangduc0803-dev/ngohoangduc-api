
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ngohoangduc.Models;
using System.ComponentModel.DataAnnotations;

namespace ngohoangduc.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Roles = "Admin")]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmployeesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _db.Employees
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return employee == null
            ? NotFound(new { message = "Không tìm thấy nhân viên" })
            : Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] EmployeeInput input)
    {
        var code = input.EmployeeCode.Trim();
        var email = input.Email.Trim();

        var exists = await _db.Employees.AnyAsync(x =>
            x.EmployeeCode == code || x.Email == email);

        if (exists)
            return Conflict(new {
                message = "Mã nhân viên hoặc email đã tồn tại"
            });

        var employee = new Employee
        {
            EmployeeCode = code,
            FullName = input.FullName.Trim(),
            Email = email,
            Department = input.Department.Trim(),
            Position = input.Position.Trim(),
            IsActive = input.IsActive
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = employee.Id },
            employee
        );
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] EmployeeInput input)
    {
        var employee = await _db.Employees.FindAsync(id);

        if (employee == null)
            return NotFound(new {
                message = "Không tìm thấy nhân viên"
            });

        var code = input.EmployeeCode.Trim();
        var email = input.Email.Trim();

        var duplicate = await _db.Employees.AnyAsync(x =>
            x.Id != id &&
            (x.EmployeeCode == code || x.Email == email));

        if (duplicate)
            return Conflict(new {
                message = "Mã nhân viên hoặc email đã tồn tại"
            });

        employee.EmployeeCode = code;
        employee.FullName = input.FullName.Trim();
        employee.Email = email;
        employee.Department = input.Department.Trim();
        employee.Position = input.Position.Trim();
        employee.IsActive = input.IsActive;

        await _db.SaveChangesAsync();

        return Ok(employee);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var employee = await _db.Employees.FindAsync(id);

        if (employee == null)
            return NotFound(new {
                message = "Không tìm thấy nhân viên"
            });

        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

public class EmployeeInput
{
    [Required]
    [StringLength(30)]
    public string EmployeeCode { get; set; } = "";

    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    public string Department { get; set; } = "";
    public string Position { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
