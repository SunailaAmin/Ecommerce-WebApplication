using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Inventory;

public class AdjustInventoryRequest
{
    /// <summary>
    /// Signed change to apply. Positive adds stock, negative removes it.
    /// </summary>
    [Required]
    [Range(-1000000, 1000000)]
    public int Delta { get; set; }
}
