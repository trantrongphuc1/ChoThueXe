using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class AdminAccountCrudInputModel
{
    public int UserId { get; set; }

    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [RegularExpression("ADMIN|EMPLOYEE|CUSTOMER", ErrorMessage = "Role khong hop le.")]
    public string RoleName { get; set; } = "CUSTOMER";

    [StringLength(100)]
    public string? Password { get; set; }
}
