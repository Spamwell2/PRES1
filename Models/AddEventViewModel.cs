using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PRES1.Models
{
    public class AddEventViewModel
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(120)]
        public string? Subtitle { get; set; }

        [Required]
        [StringLength(300)]
        public string SummaryText { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime? EventDate { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Tags { get; set; }

        [Required]
        [StringLength(100)]
        public string EventOrganiser { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string OrganiserEmail { get; set; } = string.Empty;

        public IFormFile? MainImage { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must confirm the submission is accurate.")]
        public bool ConfirmSubmission { get; set; }
    }
}