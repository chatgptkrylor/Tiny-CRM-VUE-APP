using TinyCrm.Api.Models;

namespace TinyCrm.Api.Services;

public interface IElasticSearchService
{
    /// <summary>
    /// Creates the index with the correct mapping if it does not already exist.
    /// Called once at application startup.
    /// </summary>
    Task EnsureIndexExistsAsync();

    /// <summary>
    /// Index a single customer (with its interactions) into Elasticsearch.
    /// Forces a refresh so the document is immediately searchable.
    /// </summary>
    Task IndexCustomerAsync(Customer customer);

    /// <summary>
    /// Remove a customer from the Elasticsearch index by id.
    /// Forces a refresh so the removal is immediately visible.
    /// </summary>
    Task RemoveCustomerAsync(int customerId);

    /// <summary>
    /// Bulk-index all customers (used at startup after Seed).
    /// </summary>
    Task BulkIndexAllAsync(IReadOnlyList<Customer> customers);

    /// <summary>
    /// Search for customer ids matching a search term.
    /// If status is provided, results are filtered by status.
    /// Returns (matchingIds, totalCount).
    /// </summary>
    Task<(int[] Ids, int Total)> SearchAsync(
        string? search, string? status, int page, int pageSize, bool pagingEnabled);
}
