using System.Collections.Generic;
using System.Threading.Tasks;
using WebAppTest1.Models;

namespace WebAppTest1.Services
{
    public interface IProductService
    {
        Task<List<Producto>> GetAllProductsAsync();
        Task<Producto?> GetProductByIdAsync(int id);
        Task<Producto> AddProductAsync(Producto product);
        Task<bool> UpdateProductAsync(Producto product);
        Task<bool> DeleteProductAsync(int id);
    }
}
