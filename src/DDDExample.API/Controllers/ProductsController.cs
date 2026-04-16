using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DDDExample.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        ICategoryService categoryService,
        ILogger<ProductsController> logger)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("{id:guid}", Name = "GetProductById")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        if (product is null) return NotFound();
        
        if (!string.IsNullOrEmpty(product.CategoryId))
        {
            var category = await _categoryService.GetByIdAsync(product.CategoryId, cancellationToken);
            if (category is not null)
            {
                product.CategoryName = category.Name;
            }
        }
        
        return Ok(product);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll(CancellationToken cancellationToken)
    {
        var products = (await _productService.GetAllAsync(cancellationToken)).ToList();
        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        
        var categories = new Dictionary<string, string>();
        foreach (var categoryId in categoryIds)
        {
            if (!string.IsNullOrEmpty(categoryId))
            {
                var category = await _categoryService.GetByIdAsync(categoryId, cancellationToken);
                if (category is not null)
                {
                    categories[categoryId] = category.Name;
                }
            }
        }
        
        // Actualizar los nombres de categoría en los productos
        foreach (var product in products)
        {
            if (!string.IsNullOrEmpty(product.CategoryId) && 
                categories.TryGetValue(product.CategoryId, out var categoryName))
            {
                product.CategoryName = categoryName;
            }
        }
        
        var result = products.AsEnumerable();
        
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken)
    {
        var createdProduct = await _productService.CreateAsync(dto, cancellationToken);
        return CreatedAtRoute("GetProductById", new { id = createdProduct.Id }, createdProduct);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductDto dto,
        CancellationToken cancellationToken)
    {
        await _productService.UpdateAsync(id, dto, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/stock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStock(
        Guid id,
        [FromBody] UpdateProductStockDto dto,
        CancellationToken cancellationToken)
    {
        await _productService.UpdateStockAsync(id, dto, cancellationToken);
        return NoContent();
    }
}
