using Nest;

namespace TinyCrm.Api.Models;

/// <summary>
/// Elasticsearch document for a customer. Denormalised: interaction subjects and
/// notes are concatenated so a single ES query can search across all searchable fields.
/// </summary>
public class CustomerDocument
{
    [Number(Name = "id")]
    public int Id { get; set; }

    // Edge-ngram indexed fields for as-you-type substring matching ("acm" -> "Acme").
    [Text(Name = "name", Analyzer = "edge_ngram_analyzer", SearchAnalyzer = "standard")]
    public string Name { get; set; } = string.Empty;

    [Text(Name = "company", Analyzer = "edge_ngram_analyzer", SearchAnalyzer = "standard")]
    public string? Company { get; set; }

    [Text(Name = "email", Analyzer = "edge_ngram_analyzer", SearchAnalyzer = "standard")]
    public string? Email { get; set; }

    // Standard full-text fields for word-level matching ("pricing" in notes).
    [Text(Name = "notes")]
    public string? Notes { get; set; }

    [Keyword(Name = "status")]
    public string? Status { get; set; }

    // Denormalised interaction data for full-text search across interaction fields.
    [Text(Name = "interactionSubjects")]
    public string InteractionSubjects { get; set; } = string.Empty;

    [Text(Name = "interactionNotes")]
    public string InteractionNotes { get; set; } = string.Empty;
}
