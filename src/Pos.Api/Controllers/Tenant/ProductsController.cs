using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Products.Commands;
using Pos.Application.Products.DTOs;
using Pos.Application.Products.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
[Authorize]
[EnableRateLimiting("GlobalApiPolicy")]
public class ProductsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ProductsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
        [FromQuery] GetProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductsQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.CategoryId,
            request.IsActive);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetProductByIdQuery(id);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(
            request.Name,
            request.Sku,
            request.PriceAmount,
            request.Currency,
            request.CategoryId,
            request.Description,
            request.Barcode,
            request.CostAmount,
            request.InitialStock);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(
            id,
            request.Name,
            request.Description,
            request.Barcode,
            request.CategoryId);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/price")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProductPrice(
        Guid id,
        [FromBody] UpdateProductPriceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductPriceCommand(
            id,
            request.PriceAmount,
            request.Currency);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeProductStatus(
        Guid id,
        [FromBody] ChangeStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IsActive)
        {
            var command = new ActivateProductCommand(id);
            var result = await _dispatcher.SendAsync(command, cancellationToken);
            return this.ToActionResult(result);
        }
        else
        {
            var command = new DeactivateProductCommand(id);
            var result = await _dispatcher.SendAsync(command, cancellationToken);
            return this.ToActionResult(result);
        }
    }
}
