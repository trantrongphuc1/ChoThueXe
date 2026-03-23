using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Rental;

public class CreateDraftInputModel
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [Range(1, int.MaxValue)]
    public int EmployeeId { get; set; }
}
