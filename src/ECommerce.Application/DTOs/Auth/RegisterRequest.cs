using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Auth;

public class RegisterRequest
{
    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression("^(?=.*[A-Za-z])(?=.*\\d).+$",
        ErrorMessage = "Password must contain at least one letter and one number.")]
    public string Password { get; set; } = string.Empty;
}
