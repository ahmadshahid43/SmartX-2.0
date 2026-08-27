namespace OmniBusiness.Domain.Foundation;

public enum FormFieldType
{
    ShortText,
    LongText,
    Number,
    Date,
    Dropdown,
    Formula,
    Lookup
}

public sealed record FormLibraryField(
    string Key,
    string Label,
    string Group,
    string Icon);

public sealed record FormCanvasField(
    string FieldId,
    string Label,
    FormFieldType Type,
    bool Required,
    string Placeholder,
    string? HelpText,
    string? DefaultValue,
    bool IsReadOnly,
    int? MinValue,
    int? MaxValue);

public sealed record FormDefinition(
    string Id,
    string Title,
    string Description,
    string SelectedFieldId,
    IReadOnlyList<FormLibraryField> Library,
    IReadOnlyList<FormCanvasField> Canvas);
