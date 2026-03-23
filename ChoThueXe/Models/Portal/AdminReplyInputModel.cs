using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class AdminReplyInputModel
{
    [Range(1, int.MaxValue)]
    public int MessageId { get; set; }

    [Required]
    [StringLength(1000)]
    public string ReplyContent { get; set; } = string.Empty;
}
