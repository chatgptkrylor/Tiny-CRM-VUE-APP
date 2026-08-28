using TinyCrm.Api.Models;

namespace TinyCrm.Api.Dtos;

public record StatusSummaryItem(CustomerStatus Status, int Count);

public record InteractionTypeSummaryItem(InteractionType Type, int Count);

public record CustomerReportItem(
    int Id, string Name, string? Company, CustomerStatus Status, int InteractionCount, DateTime? LastInteractionDate);

public record ReportsResponse(
    List<StatusSummaryItem> StatusSummary,
    List<InteractionTypeSummaryItem> InteractionTypeSummary,
    List<CustomerReportItem> Customers);
