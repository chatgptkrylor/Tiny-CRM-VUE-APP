using Microsoft.EntityFrameworkCore;
using Nest;
using TinyCrm.Api.Data;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Services;

public class ElasticSearchService : IElasticSearchService
{
    private readonly IElasticClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string IndexName = "tinycrm-customers";

    public ElasticSearchService(IElasticClient client, IServiceScopeFactory scopeFactory)
    {
        _client = client;
        _scopeFactory = scopeFactory;
    }

    public async Task EnsureIndexExistsAsync()
    {
        var existsResponse = await _client.Indices.ExistsAsync(IndexName);
        if (existsResponse.Exists) return;

        var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
            .Settings(s => s
                .NumberOfReplicas(0)
                .NumberOfShards(1)
                .Analysis(a => a
                    .TokenFilters(f => f
                        .EdgeNGram("edge_ngram_filter", e => e
                            .MinGram(2)
                            .MaxGram(20)))
                    .Analyzers(an => an
                        .Custom("edge_ngram_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filters("lowercase", "edge_ngram_filter"))
                        .Custom("search_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filters("lowercase")))))
            .Map<CustomerDocument>(m => m
                .AutoMap()
                .Properties(p => p
                    .Number(n => n.Name("id").Type(NumberType.Integer))
                    .Text(t => t.Name("name").Analyzer("edge_ngram_analyzer").SearchAnalyzer("search_analyzer"))
                    .Text(t => t.Name("company").Analyzer("edge_ngram_analyzer").SearchAnalyzer("search_analyzer"))
                    .Text(t => t.Name("email").Analyzer("edge_ngram_analyzer").SearchAnalyzer("search_analyzer"))
                    .Text(t => t.Name("notes"))
                    .Keyword(k => k.Name("status"))
                    .Text(t => t.Name("interactionSubjects"))
                    .Text(t => t.Name("interactionNotes")))));

        if (!createResponse.IsValid)
            throw new InvalidOperationException(
                "Failed to create ES index: " + (createResponse.ServerError?.Error?.Reason ?? createResponse.DebugInformation));
    }

    public async Task IndexCustomerAsync(Customer customer)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();

        var dbCustomer = await db.Customers
            .AsNoTracking()
            .Include(c => c.Interactions)
            .FirstOrDefaultAsync(c => c.Id == customer.Id);

        if (dbCustomer is null)
        {
            await RemoveCustomerAsync(customer.Id);
            return;
        }

        var doc = MapToDocument(dbCustomer);
        var response = await _client.IndexAsync(doc, d => d
            .Index(IndexName)
            .Id(doc.Id)
            .Refresh(Elasticsearch.Net.Refresh.WaitFor));

        if (!response.IsValid)
            throw new InvalidOperationException(
                "Failed to index customer " + customer.Id + ": " +
                (response.ServerError?.Error?.Reason ?? response.DebugInformation));
    }

    public async Task RemoveCustomerAsync(int customerId)
    {
        var response = await _client.DeleteAsync<CustomerDocument>(customerId, d => d
            .Index(IndexName)
            .Refresh(Elasticsearch.Net.Refresh.True));

        // 404 is acceptable: the document may not exist yet.
        if (!response.IsValid && response.ApiCall?.HttpStatusCode != 404)
            throw new InvalidOperationException(
                "Failed to remove customer " + customerId + " from ES: " +
                (response.ServerError?.Error?.Reason ?? response.DebugInformation));
    }

    public async Task BulkIndexAllAsync(IReadOnlyList<Customer> customers)
    {
        if (customers.Count == 0) return;

        var documents = customers.Select(MapToDocument).ToList();

        var response = await _client.BulkAsync(b => b
            .IndexMany(documents, (op, doc) => op
                .Document(doc)
                .Index(IndexName)
                .Id(doc.Id))
            .Refresh(Elasticsearch.Net.Refresh.False));

        if (!response.IsValid)
            throw new InvalidOperationException(
                "Bulk index failed: " + (response.ServerError?.Error?.Reason ?? response.DebugInformation));
    }

    public async Task<(int[] Ids, int Total)> SearchAsync(
        string? search, string? status, int page, int pageSize, bool pagingEnabled)
    {
        var size = pagingEnabled ? Math.Min(pageSize, 200) : 10_000;
        var from = pagingEnabled ? (page - 1) * size : 0;

        var response = await _client.SearchAsync<CustomerDocument>(s => s
            .Index(IndexName)
            .Size(size)
            .From(from)
            .TrackScores(false)
            .Query(q =>
            {
                QueryContainer query = null!;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    if (term is not "%" and not "_")
                    {
                        query = q.MultiMatch(m => m
                            .Fields(f => f
                                .Field(c => c.Name, 2.0)
                                .Field(c => c.Company, 1.5)
                                .Field(c => c.Email, 1.5)
                                .Field(c => c.Notes, 1.0)
                                .Field(c => c.InteractionSubjects, 1.0)
                                .Field(c => c.InteractionNotes, 1.0))
                            .Query(term)
                            .Type(TextQueryType.BestFields));
                    }
                    else
                    {
                        query = q.MatchNone();
                    }
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var statusQuery = q.Term(t => t.Field(f => f.Status).Value(status));
                    query = query is null ? statusQuery : q.Bool(b => b.Filter(query, statusQuery));
                }

                query ??= q.MatchAll();
                return query;
            }));

        if (!response.IsValid)
            throw new InvalidOperationException(
                "ES search failed: " + (response.ServerError?.Error?.Reason ?? response.DebugInformation));

        var ids = response.Hits.Select(h => (int)h.Source.Id).ToArray();
        var total = (int)response.Total;
        return (ids, total);
    }

    private static CustomerDocument MapToDocument(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        Company = customer.Company,
        Email = customer.Email,
        Notes = customer.Notes,
        Status = customer.Status.ToString(),
        InteractionSubjects = string.Join(" ",
            customer.Interactions.Select(i => i.Subject).Where(s => !string.IsNullOrEmpty(s))),
        InteractionNotes = string.Join(" ",
            customer.Interactions.Select(i => i.Notes).Where(n => !string.IsNullOrEmpty(n))),
    };
}
