using System.ComponentModel.DataAnnotations;

namespace ECommerce.Products.Application.DTOs.Inventory;

public class UpdateInventoryRequest
{
    [Required]
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}
