using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class SubmitDocumentInputModel
{
    [Required]
    public string DocType { get; set; } = "CCCD";

    public string FileUrl { get; set; } = string.Empty;
}
