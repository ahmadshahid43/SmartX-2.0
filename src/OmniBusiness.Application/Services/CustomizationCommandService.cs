using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Contracts;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Services;

public sealed class CustomizationCommandService(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceQueryService workspaceQueryService) : ICustomizationCommandService
{
    public async Task<FormBuilderDto> AddProductCustomFieldAsync(
        Guid tenantId,
        SaveFormFieldRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var nextField = ToField(
                request,
                BuildFieldId(request.Label, snapshot.ProductCustomFields.Canvas));

            return snapshot with
            {
                ProductCustomFields = snapshot.ProductCustomFields with
                {
                    SelectedFieldId = nextField.FieldId,
                    Canvas = snapshot.ProductCustomFields.Canvas
                        .Append(nextField)
                        .ToArray()
                }
            };
        }, cancellationToken);

        return await workspaceQueryService.GetProductCustomFieldsAsync(tenantId, cancellationToken);
    }

    public async Task<FormBuilderDto> UpdateProductCustomFieldAsync(
        Guid tenantId,
        string fieldId,
        SaveFormFieldRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            if (!snapshot.ProductCustomFields.Canvas.Any(field => field.FieldId == fieldId))
            {
                throw new InvalidOperationException("The selected custom field was not found.");
            }

            return snapshot with
            {
                ProductCustomFields = snapshot.ProductCustomFields with
                {
                    SelectedFieldId = fieldId,
                    Canvas = snapshot.ProductCustomFields.Canvas
                        .Select(field => field.FieldId == fieldId ? ToField(request, fieldId) : field)
                        .ToArray()
                }
            };
        }, cancellationToken);

        return await workspaceQueryService.GetProductCustomFieldsAsync(tenantId, cancellationToken);
    }

    public async Task<FormBuilderDto> DeleteProductCustomFieldAsync(
        Guid tenantId,
        string fieldId,
        CancellationToken cancellationToken)
    {
        await workspaceRepository.UpdateWorkspaceSnapshotAsync(snapshot =>
        {
            EnsureTenant(snapshot, tenantId);

            var remaining = snapshot.ProductCustomFields.Canvas
                .Where(field => field.FieldId != fieldId)
                .ToArray();

            var selectedFieldId = remaining.FirstOrDefault()?.FieldId ?? string.Empty;

            return snapshot with
            {
                ProductCustomFields = snapshot.ProductCustomFields with
                {
                    SelectedFieldId = selectedFieldId,
                    Canvas = remaining
                }
            };
        }, cancellationToken);

        return await workspaceQueryService.GetProductCustomFieldsAsync(tenantId, cancellationToken);
    }

    private static FormCanvasField ToField(SaveFormFieldRequestDto request, string fieldId)
    {
        if (!Enum.TryParse<FormFieldType>(request.Type, true, out var fieldType))
        {
            throw new InvalidOperationException($"Unsupported field type '{request.Type}'.");
        }

        return new FormCanvasField(
            fieldId,
            request.Label.Trim(),
            fieldType,
            request.Required,
            request.Placeholder?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(request.HelpText) ? null : request.HelpText.Trim(),
            string.IsNullOrWhiteSpace(request.DefaultValue) ? null : request.DefaultValue.Trim(),
            request.IsReadOnly,
            request.MinValue,
            request.MaxValue);
    }

    private static string BuildFieldId(string label, IEnumerable<FormCanvasField> existingFields)
    {
        var baseSlug = new string(label
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray())
            .Trim('_');

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "custom_field";
        }

        var candidate = $"fld_{baseSlug}";
        var suffix = 1;

        while (existingFields.Any(field => string.Equals(field.FieldId, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            suffix++;
            candidate = $"fld_{baseSlug}_{suffix}";
        }

        return candidate;
    }

    private static void ValidateRequest(SaveFormFieldRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            throw new InvalidOperationException("Field label is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            throw new InvalidOperationException("Field type is required.");
        }
    }

    private static void EnsureTenant(WorkspaceSnapshot snapshot, Guid tenantId)
    {
        if (snapshot.Tenant.Id != tenantId)
        {
            throw new InvalidOperationException("The current user does not belong to the requested tenant.");
        }
    }
}
