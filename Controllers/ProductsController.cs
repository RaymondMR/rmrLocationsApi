using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Authorization;
using WebAppTest1.Models;
using WebAppTest1.Services;

namespace WebAppTest1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _service;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IProductService service, ILogger<ProductsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<Producto>>> GetAllProducts()
        {
            var products = await _service.GetAllProductsAsync();
            return Ok(products);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Producto>> GetProductById(int id)
        {
            var product = await _service.GetProductByIdAsync(id);
            if (product == null)
                return NotFound();

            return Ok(product);
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<ActionResult<Producto>> CreateProduct([FromBody] Producto product)
        {
            if (product == null)
                return BadRequest();

            var created = await _service.AddProductAsync(product);
            return CreatedAtAction(nameof(GetProductById), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Producto product)
        {
            if (product == null)
                return BadRequest();

            if (id != product.Id)
            {
                // ensure route id and body id are consistent
                product.Id = id;
            }

            // check existence
            var exists = await _service.GetProductByIdAsync(id);
            if (exists == null)
                return NotFound();

            var updated = await _service.UpdateProductAsync(product);
            if (!updated)
            {
                _logger.LogWarning("Failed to update product {ProductId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update the product.");
            }

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var deleted = await _service.DeleteProductAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        [HttpPatch("{id:int}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> PatchProduct(int id, [FromBody] JsonPatchDocument<Producto> patchDoc)
        {
            if (patchDoc == null)
                return BadRequest();

            var existing = await _service.GetProductByIdAsync(id);
            if (existing == null)
                return NotFound();

            // Create a copy/DTO to apply patch to (avoid applying directly to tracked entity if you want validation)
            var dto = new Producto
            {
                Id = existing.Id,
                Nombre = existing.Nombre,
                Precio = existing.Precio,
                Stock = existing.Stock
            };

            patchDoc.ApplyTo(dto, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            // Optional: run additional DTO validation (e.g., data annotations)
            // Map DTO back to entity (or use dto as entity) and persist
            var updated = await _service.UpdateProductAsync(dto);
            if (!updated)
            {
                _logger.LogWarning("Failed to patch product {ProductId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update the product.");
            }

            return NoContent();
        }
    }
}
