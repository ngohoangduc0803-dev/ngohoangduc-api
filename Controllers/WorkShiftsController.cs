
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ngohoangduc.Models;

namespace ngohoangduc.Controllers;

[ApiController]
[Route("api/workshifts")]
[Authorize]
public class WorkShiftsController : ControllerBase
{
    private readonly AppDbContext _db;

    public WorkShiftsController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/workshifts
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var shifts = await _db.WorkShifts
            .AsNoTracking()
            .ToListAsync();

        return Ok(shifts);
    }

    // GET: api/workshifts/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var shift = await _db.WorkShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (shift == null)
            return NotFound();

        return Ok(shift);
    }

    // POST: api/workshifts
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] WorkShift input)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        _db.WorkShifts.Add(input);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = input.Id },
            input
        );
    }

    // PUT: api/workshifts/1
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] WorkShift input)
    {
        if (id != input.Id)
            return BadRequest("ID không khớp.");

        var existing = await _db.WorkShifts.FindAsync(id);

        if (existing == null)
            return NotFound();

        _db.Entry(existing).CurrentValues.SetValues(input);

        await _db.SaveChangesAsync();

        return Ok(existing);
    }

    // DELETE: api/workshifts/1
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var shift = await _db.WorkShifts.FindAsync(id);

        if (shift == null)
            return NotFound();

        _db.WorkShifts.Remove(shift);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
