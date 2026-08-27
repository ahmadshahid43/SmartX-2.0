using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

public interface IModuleManagementService
{
    Task<ModuleSettingsDto> UpdateModuleSettingsAsync(
        Guid tenantId,
        SaveModuleSettingsRequestDto request,
        CancellationToken cancellationToken);
}
