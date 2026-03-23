using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Auth;

public class RegisterInputModel
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Mat khau toi thieu 8 ky tu, gom chu hoa, chu thuong va so.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Mat khau nhap lai khong khop.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
