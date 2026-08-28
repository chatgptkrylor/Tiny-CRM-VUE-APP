using TinyCrm.Api.Models;

namespace TinyCrm.Api.Dtos;

public record InteractionItem(
    int Id, int CustomerId, InteractionType Type, string Subject, string? Notes,
    DateTime InteractionDate, DateTime CreatedAt);
