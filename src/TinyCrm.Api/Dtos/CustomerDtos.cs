using TinyCrm.Api.Models;

namespace TinyCrm.Api.Dtos;

public record CustomerListItem(
    int Id, string Name, string? Company, string? Email, string? Phone,
    CustomerStatus Status, DateTime? LastInteractionDate, int InteractionCount);

public record CustomerDetail(
    int Id, string Name, string? Company, string? Email, string? Phone,
    CustomerStatus Status, string? Notes, DateTime CreatedAt, DateTime? LastInteractionDate,
    List<InteractionItem> Interactions);
