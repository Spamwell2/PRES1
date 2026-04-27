using System.ComponentModel.DataAnnotations;

namespace PRES1.Models
{
    public class ContactFormViewModel
    {
        [Required]
        public string Name { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Subject { get; set; }
        [Required]
        [MaxLength(1200)]
        public string Message { get; set; }
    }
}
