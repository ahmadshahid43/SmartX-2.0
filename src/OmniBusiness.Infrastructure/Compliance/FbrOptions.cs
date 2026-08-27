namespace OmniBusiness.Infrastructure.Compliance;

public sealed class FbrOptions
{
    public const string SectionName = "Fbr";

    public string Mode { get; init; } = "OfflineQueue";

    public string SellerId { get; init; } = string.Empty;

    public string BearerToken { get; init; } = string.Empty;
}
