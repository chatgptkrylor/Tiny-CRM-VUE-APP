using System.ComponentModel.DataAnnotations;

namespace TinyCrm.Api.Models;

public class Interaction
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public InteractionType Type { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Subject must be between 3 and 200 characters.")]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime InteractionDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
