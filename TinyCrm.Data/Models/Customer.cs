using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TinyCrm.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [StringLength(150)]
        [Display(Name = "Company")]
        public string Company { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(150)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [StringLength(50)]
        [Display(Name = "Phone")]
        [RegularExpression(@"^[0-9+\-\s()]{0,50}$", ErrorMessage = "Phone may contain digits, spaces and + - ( ) only.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [Display(Name = "Status")]
        public CustomerStatus Status { get; set; }

        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Notes")]
        public string Notes { get; set; }

        [Display(Name = "Created")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Interaction")]
        public DateTime? LastInteractionDate { get; set; }

        public IList<Interaction> Interactions { get; set; } = new List<Interaction>();
    }
}
