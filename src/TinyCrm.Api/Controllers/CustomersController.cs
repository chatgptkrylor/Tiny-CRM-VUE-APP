using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly TinyCrmDbContext _db;
    public CustomersController(TinyCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<CustomerListItem>>> List(
        [FromQuery] string? search, [FromQuery] string? status)
    {
        var q = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            // Translated to SQL LIKE — case-insensitive under the default collation (D1).
            q = q.Where(c => EF.Functions.Like(c.Name, $"%{s}%")
                          || (c.Email != null   && EF.Functions.Like(c.Email, $"%{s}%"))
                          || (c.Company != null && EF.Functions.Like(c.Company, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CustomerStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);

        return await q.OrderBy(c => c.Id)
            .Select(c => new CustomerListItem(
                c.Id, c.Name, c.Company, c.Email, c.Phone,
                c.Status, c.LastInteractionDate, c.Interactions.Count()))
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDetail>> Get(int id)
    {
        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetail(
                c.Id, c.Name, c.Company, c.Email, c.Phone, c.Status, c.Notes,
                c.CreatedAt, c.LastInteractionDate,
                // D2: date DESC, then id DESC — deterministic tie-break for same-day interactions.
                c.Interactions
                    .OrderByDescending(i => i.InteractionDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => new InteractionItem(
                        i.Id, i.CustomerId, i.Type, i.Subject, i.Notes, i.InteractionDate, i.CreatedAt))
                    .ToList()))
            .FirstOrDefaultAsync();

        return customer is null ? NotFound() : customer;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDetail>> Create([FromBody] Customer model)
    {
        // Invalid ModelState (DataAnnotations on Customer) already yields the
        // automatic 400 ValidationProblemDetails via [ApiController] before this runs.
        var entity = new Customer
        {
            Name = model.Name,
            Company = model.Company,
            Email = model.Email,
            Phone = model.Phone,
            Status = model.Status,
            Notes = model.Notes,
            CreatedAt = DateTime.Now,
        };
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();
        return await Get(entity.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Customer model)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        customer.Name = model.Name;
        customer.Company = model.Company;
        customer.Email = model.Email;
        customer.Phone = model.Phone;
        customer.Status = model.Status;
        customer.Notes = model.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        // Children cascade at the database level (TinyCrmDbContext.OnModelCreating).
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
