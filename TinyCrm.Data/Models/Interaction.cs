using System;
using System.ComponentModel.DataAnnotations;

namespace TinyCrm.Models
{
    public class Interaction
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Type is required.")]
        [Display(Name = "Type")]
        public InteractionType Type { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Subject must be between 3 and 200 characters.")]
        [Display(Name = "Subject")]
        public string Subject { get; set; }

        [DataType(DataType.MultilineText)]
        [StringLength(2000)]
        [Display(Name = "Notes")]
        public string Notes { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime InteractionDate { get; set; }

        [Display(Name = "Logged")]
        public DateTime CreatedAt { get; set; }

        // Display-only convenience property, not part of the EDMX model
        // and therefore not persisted. Filled in by the repositories.
        [Display(Name = "Customer")]
        public string CustomerName { get; set; }
    }
}
