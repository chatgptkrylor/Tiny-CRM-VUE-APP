using System.ComponentModel.DataAnnotations;

namespace TinyCrm.Api.Models;

public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Company { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(50)]
    [RegularExpression(@"^[0-9+\-\s()]{0,50}$", ErrorMessage = "Phone may contain digits, spaces and + - ( ) only.")]
    public string? Phone { get; set; }

    public CustomerStatus Status { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastInteractionDate { get; set; }

    public List<Interaction> Interactions { get; set; } = new();
}
