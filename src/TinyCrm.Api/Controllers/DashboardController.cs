using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly TinyCrmDbContext _db;
    public DashboardController(TinyCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get()
    {
        var totalCustomers = await _db.Customers.CountAsync();
        var totalInteractions = await _db.Interactions.CountAsync();

        // Grouped/counted in the database; only the (at most 3/4-row) summary is
        // pulled into memory to backfill enum values with zero counts.
        var statusCounts = await _db.Customers
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var customersByStatus = Enum.GetValues<CustomerStatus>()
            .ToDictionary(s => s.ToString(), s => statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0);

        var typeCounts = await _db.Interactions
            .GroupBy(i => i.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();
        var interactionsByType = Enum.GetValues<InteractionType>()
            .ToDictionary(t => t.ToString(), t => typeCounts.FirstOrDefault(x => x.Type == t)?.Count ?? 0);

        var recentInteractions = await _db.Interactions
            .OrderByDescending(i => i.InteractionDate)
            .Take(5)
            .Select(i => new RecentInteractionItem(
                i.Id, i.CustomerId, i.Customer!.Name, i.Type, i.Subject, i.InteractionDate))
            .ToListAsync();

        var cutoff = DateTime.Today.AddDays(-30);
        var needsFollowUps = await _db.Customers
            .CountAsync(c => c.LastInteractionDate == null || c.LastInteractionDate < cutoff);

        return new DashboardResponse(
            totalCustomers, totalInteractions, customersByStatus, interactionsByType,
            recentInteractions, needsFollowUps);
    }
}
