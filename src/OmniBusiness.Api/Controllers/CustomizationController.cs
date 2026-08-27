using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniBusiness.Api.Infrastructure;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Application.Services;

namespace OmniBusiness.Api.Controllers;

[ApiController]
[Authorize(Roles = "Owner")]
[Route("api/v1/customization")]
public sealed class CustomizationController(
    IWorkspaceQueryService workspaceQueryService,
    ICustomizationCommandService customizationCommandService) : ControllerBase
{
    [HttpGet("forms/product-custom-fields")]
    [ProducesResponseType<FormBuilderDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FormBuilderDto>> GetProductCustomFieldsAsync(CancellationToken cancellationToken)
    {
        var form = await workspaceQueryService.GetProductCustomFieldsAsync(User.GetTenantId(), cancellationToken);
        return Ok(form);
    }

    [HttpPost("forms/product-custom-fields/fields")]
    [ProducesResponseType<FormBuilderDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FormBuilderDto>> AddProductCustomFieldAsync(
        [FromBody] SaveFormFieldRequestDto request,
        CancellationToken cancellationToken)
    {
        var form = await customizationCommandService.AddProductCustomFieldAsync(
            User.GetTenantId(),
            request,
            cancellationToken);

        return Ok(form);
    }

    [HttpPut("forms/product-custom-fields/fields/{fieldId}")]
    [ProducesResponseType<FormBuilderDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FormBuilderDto>> UpdateProductCustomFieldAsync(
        string fieldId,
        [FromBody] SaveFormFieldRequestDto request,
        CancellationToken cancellationToken)
    {
        var form = await customizationCommandService.UpdateProductCustomFieldAsync(
            User.GetTenantId(),
            fieldId,
            request,
            cancellationToken);

        return Ok(form);
    }

    [HttpDelete("forms/product-custom-fields/fields/{fieldId}")]
    [ProducesResponseType<FormBuilderDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FormBuilderDto>> DeleteProductCustomFieldAsync(
        string fieldId,
        CancellationToken cancellationToken)
    {
        var form = await customizationCommandService.DeleteProductCustomFieldAsync(
            User.GetTenantId(),
            fieldId,
            cancellationToken);

        return Ok(form);
    }
}
