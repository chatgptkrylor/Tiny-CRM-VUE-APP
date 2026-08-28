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
}
