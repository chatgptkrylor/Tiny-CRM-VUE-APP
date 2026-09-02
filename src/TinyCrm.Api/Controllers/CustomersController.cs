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

    // Largest page a caller may request. Without a cap, ?pageSize=99999999 is a
    // cheap way to make the server materialise the whole table into memory.
    private const int MaxPageSize = 200;

    [HttpGet]
    public async Task<ActionResult<List<CustomerListItem>>> List(
        [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int? pageSize = null)
    {
        var q = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            // ILIKE, not LIKE: SQL Server's default collation made LIKE case-insensitive,
            // but Postgres LIKE is case-SENSITIVE, which would silently undo D1. ILIKE is
            // the Postgres equivalent and keeps the same % / _ wildcard passthrough (D7).
            q = q.Where(c => EF.Functions.ILike(c.Name, $"%{s}%")
                          || (c.Email != null   && EF.Functions.ILike(c.Email, $"%{s}%"))
                          || (c.Company != null && EF.Functions.ILike(c.Company, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CustomerStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);

        IQueryable<Customer> ordered = q.OrderBy(c => c.Id);

        // Paging is OPT-IN: with no pageSize the response is the full list, byte for
        // byte what it was before. That keeps the payload a bare JSON array, which
        // every existing caller (and CustomersTests) deserialises as List<CustomerListItem>.
        if (pageSize is > 0)
        {
            var size = Math.Min(pageSize.Value, MaxPageSize);
            var p = page < 1 ? 1 : page;
            // The count is the filtered total, taken before Skip/Take, so the client can
            // render "page 3 of 101". It only runs when paging was actually asked for.
            Response.Headers["X-Total-Count"] = (await q.CountAsync()).ToString();
            // Widened to long first: ?page=2147483647 would overflow int here and make
            // Skip throw, turning a silly query string into a 500.
            var skip = (long)(p - 1) * size;
            ordered = ordered.Skip((int)Math.Min(skip, int.MaxValue)).Take(size);
        }

        return await ordered
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
