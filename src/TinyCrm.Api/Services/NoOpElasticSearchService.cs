using TinyCrm.Api.Models;

namespace TinyCrm.Api.Services;

/// <summary>
/// No-op implementation used when Elasticsearch is not configured (e.g. in tests).
/// Writes are silently ignored; searches return an empty result so the caller
/// falls back to the existing EF Core query.
/// </summary>
public class NoOpElasticSearchService : IElasticSearchService
{
    public Task EnsureIndexExistsAsync() => Task.CompletedTask;
    public Task IndexCustomerAsync(Customer customer) => Task.CompletedTask;
    public Task RemoveCustomerAsync(int customerId) => Task.CompletedTask;
    public Task BulkIndexAllAsync(IReadOnlyList<Customer> customers) => Task.CompletedTask;

    /// <summary>
    /// Returns empty results to signal the controller to fall back to EF ILIKE.
    /// </summary>
    public Task<(int[] Ids, int Total)> SearchAsync(
        string? search, string? status, int page, int pageSize, bool pagingEnabled)
    {
        return Task.FromResult<(int[] Ids, int Total)>((Array.Empty<int>(), 0));
    }
}
