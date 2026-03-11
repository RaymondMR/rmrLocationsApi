using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebAppTest1.Data;
using WebAppTest1.Models;

namespace WebAppTest1.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProductService> _logger;

        public ProductService(AppDbContext db, ILogger<ProductService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<Producto>> GetAllProductsAsync()
        {
            return await _db.Productos.AsNoTracking().ToListAsync();
        }

        public async Task<Producto?> GetProductByIdAsync(int id)
        {
            return await _db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Producto> AddProductAsync(Producto product)
        {
            _db.Productos.Add(product);
            await _db.SaveChangesAsync();
            return product;
        }

        public async Task<bool> UpdateProductAsync(Producto product)
        {
            var existing = await _db.Productos.FindAsync(product.Id);
            if (existing == null)
            {
                return false;
            }

            existing.Nombre = product.Nombre;
            existing.Precio = product.Precio;
            existing.Stock = product.Stock;

            try
            {
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating product {ProductId}", product.Id);
                return false;
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var existing = await _db.Productos.FindAsync(id);
            if (existing == null)
            {
                return false;
            }

            _db.Productos.Remove(existing);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
