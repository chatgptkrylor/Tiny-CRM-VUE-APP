using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly TinyCrmDbContext _db;
    public ReportsController(TinyCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<ReportsResponse>> Get()
    {
        var statusCounts = await _db.Customers
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var statusSummary = Enum.GetValues<CustomerStatus>()
            .Select(s => new StatusSummaryItem(s, statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .ToList();

        var typeCounts = await _db.Interactions
            .GroupBy(i => i.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();
        var interactionTypeSummary = Enum.GetValues<InteractionType>()
            .Select(t => new InteractionTypeSummaryItem(t, typeCounts.FirstOrDefault(x => x.Type == t)?.Count ?? 0))
            .ToList();

        var customers = await _db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new CustomerReportItem(
                c.Id, c.Name, c.Company, c.Status, c.Interactions.Count(), c.LastInteractionDate))
            .ToListAsync();

        return new ReportsResponse(statusSummary, interactionTypeSummary, customers);
    }

    [HttpGet("customers.csv")]
    public async Task<IActionResult> ExportCsv()
    {
        var rows = await _db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Company,
                c.Email,
                c.Phone,
                c.Status,
                InteractionCount = c.Interactions.Count(),
                c.LastInteractionDate,
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append("Id,Name,Company,Email,Phone,Status,InteractionCount,LastInteraction\r\n");

        foreach (var c in rows)
        {
            var lastInteraction = c.LastInteractionDate.HasValue
                ? c.LastInteractionDate.Value.ToString("yyyy-MM-dd")
                : "";

            sb.Append(string.Join(",",
                c.Id,
                CsvEscape(c.Name),
                CsvEscape(c.Company),
                CsvEscape(c.Email),
                CsvEscape(c.Phone),
                c.Status.ToString(),
                c.InteractionCount,
                lastInteraction));
            sb.Append("\r\n");
        }

        Response.Headers["Content-Disposition"] = "attachment;filename=customers.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv");
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
