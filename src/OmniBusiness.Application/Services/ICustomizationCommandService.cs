using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface ICustomizationCommandService
{
    Task<FormBuilderDto> AddProductCustomFieldAsync(
        Guid tenantId,
        SaveFormFieldRequestDto request,
        CancellationToken cancellationToken);

    Task<FormBuilderDto> UpdateProductCustomFieldAsync(
        Guid tenantId,
        string fieldId,
        SaveFormFieldRequestDto request,
        CancellationToken cancellationToken);

    Task<FormBuilderDto> DeleteProductCustomFieldAsync(
        Guid tenantId,
        string fieldId,
        CancellationToken cancellationToken);
}
