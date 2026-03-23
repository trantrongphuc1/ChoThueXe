using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class ReviewDocumentsInputModel
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    public bool IsMatched { get; set; }
}
