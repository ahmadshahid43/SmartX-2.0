namespace OmniBusiness.Api.Infrastructure;

public sealed record ApiErrorResponse(
    bool Success,
    string Code,
    string Message,
    IReadOnlyList<string> Errors);
