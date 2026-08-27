using Microsoft.Extensions.Options;
using OmniBusiness.Application.Abstractions.Compliance;
using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Infrastructure.Compliance;

public sealed class OfflineCapableFbrInvoiceService(
    IOptions<FbrOptions> options) : IFbrInvoiceService
{
    private readonly FbrOptions _options = options.Value;

    public Task<FbrSubmissionResult> SubmitSaleAsync(
        Company company,
        SaleRecord sale,
        CancellationToken cancellationToken)
    {
        var mode = _options.Mode.Trim().ToLowerInvariant();

        return Task.FromResult(mode switch
        {
            "disabled" => new FbrSubmissionResult(
                "NotConfigured",
                null,
                "FBR submission is disabled in configuration.",
                null),
            "simulatedapproved" => new FbrSubmissionResult(
                "Submitted",
                $"SIM-{company.Country[..Math.Min(company.Country.Length, 3)].ToUpperInvariant()}-{sale.ReferenceNo}",
                null,
                DateTimeOffset.Now),
            "live" when string.IsNullOrWhiteSpace(_options.SellerId) || string.IsNullOrWhiteSpace(_options.BearerToken) =>
                new FbrSubmissionResult(
                    "CredentialsMissing",
                    null,
                    "FBR live mode needs SellerId and BearerToken before submission can start.",
                    null),
            "live" => new FbrSubmissionResult(
                "ReadyForLiveAdapter",
                null,
                "Live FBR adapter seam is configured, but remote submission is intentionally disabled in this offline build.",
                null),
            _ => new FbrSubmissionResult(
                "QueuedOffline",
                null,
                "Invoice queued for later FBR submission when internet and credentials are available.",
                null)
        });
    }
}
