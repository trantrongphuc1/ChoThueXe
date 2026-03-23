using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class VehicleReviewInputModel
{
    [Range(1, int.MaxValue)]
    public int ContractId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string Comment { get; set; } = string.Empty;
}
