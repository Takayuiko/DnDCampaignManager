using System.ComponentModel.DataAnnotations;

namespace DnDCampingManager.Api.DTOs
{
    public class RegisterRequestDto
    {
        private string _email = string.Empty;
        [Required, EmailAddress, StringLength(254)]
        public string Email { get => _email; set => _email = value?.Trim() ?? string.Empty; }

        [Required, StringLength(128, MinimumLength = 12,
            ErrorMessage = "Password must be between 12 and 128 characters.")]
        public string Password { get; set; } = string.Empty;
    }
}

