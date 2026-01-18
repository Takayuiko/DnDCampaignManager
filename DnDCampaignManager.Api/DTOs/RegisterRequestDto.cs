using System.ComponentModel.DataAnnotations;

namespace DnDCampingManager.Api.DTOs
{
    public class RegisterRequestDto
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}

