using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class CustomerMessageInputModel
{
    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;
}
