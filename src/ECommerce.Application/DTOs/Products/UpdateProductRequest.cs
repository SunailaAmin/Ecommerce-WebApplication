using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Products;

public class UpdateProductRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal Price { get; set; }

    [Required]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public bool IsActive { get; set; } = true;
}
