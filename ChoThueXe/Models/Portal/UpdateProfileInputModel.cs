using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class UpdateProfileInputModel
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;
}
