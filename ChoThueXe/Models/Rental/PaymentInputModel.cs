using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Rental;

public class PaymentInputModel
{
    [Range(1, int.MaxValue)]
    public int ContractId { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Amount { get; set; }
}
