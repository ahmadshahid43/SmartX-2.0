namespace OmniBusiness.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; init; } = "LocalJson";

    public string SeedPath { get; init; } = "Data/foundation.json";

    public string LocalDataPath { get; init; } = @"%LOCALAPPDATA%\SmartX\foundation.local.json";

    public bool InitializeFromSeedOnFirstRun { get; init; } = true;

    /// <summary>
    /// Postgres connection string used only by the "Supabase"/"Postgres" providers.
    /// Never committed with a value; supplied at runtime via the
    /// <c>Persistence__ConnectionString</c> environment variable. The connection
    /// should authenticate as a dedicated least-privilege application role
    /// (NOT the Supabase <c>service_role</c> JWT) and set <c>sslmode=require</c>.
    /// Ignored by the LocalJson/EmbeddedSeed providers.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
