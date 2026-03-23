using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Auth;

public class LoginInputModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
