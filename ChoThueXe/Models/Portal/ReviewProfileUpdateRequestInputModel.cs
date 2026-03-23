using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class ReviewProfileUpdateRequestInputModel
{
    [Range(1, int.MaxValue)]
    public int RequestId { get; set; }

    public bool IsApproved { get; set; }
}
