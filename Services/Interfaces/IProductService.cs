using Web.Models;

namespace Web.Services.Interfaces
{
    public interface IProductService
    {
        Task<List<ProductListDto>> GetAllProductsAsync(string? category = null, string? brand = null, string? search = null, string? sort = null);
        Task<ProductDetailDto?> GetProductByIdAsync(int id);
        Task<ProductDetailDto?> GetProductBySlugAsync(string slug);
        Task<ProductListDto> CreateProductAsync(CreateProductDto dto);
        Task<ProductDetailDto?> UpdateProductAsync(int id, UpdateProductDto dto);
        Task<bool> DeleteProductAsync(int id);
        Task<bool> UpdateStockAsync(int id, int addedQuantity, int? variantId = null);
    }
}
