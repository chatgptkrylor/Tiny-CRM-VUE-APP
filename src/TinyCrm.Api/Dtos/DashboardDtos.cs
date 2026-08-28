using TinyCrm.Api.Models;

namespace TinyCrm.Api.Dtos;

public record RecentInteractionItem(
    int Id, int CustomerId, string CustomerName, InteractionType Type, string Subject, DateTime InteractionDate);

public record DashboardResponse(
    int TotalCustomers,
    int TotalInteractions,
    Dictionary<string, int> CustomersByStatus,
    Dictionary<string, int> InteractionsByType,
    List<RecentInteractionItem> RecentInteractions,
    int NeedsFollowUps);
