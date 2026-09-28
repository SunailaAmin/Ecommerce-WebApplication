using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Inventory;

public class UpdateInventoryRequest
{
    [Required]
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}
