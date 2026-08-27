using OmniBusiness.Domain.Foundation;

namespace OmniBusiness.Application.Abstractions.Compliance;

public interface IFbrInvoiceService
{
    Task<FbrSubmissionResult> SubmitSaleAsync(
        Company company,
        SaleRecord sale,
        CancellationToken cancellationToken);
}

public sealed record FbrSubmissionResult(
    string Status,
    string? InvoiceNumber,
    string? ErrorMessage,
    DateTimeOffset? ReportedAt);
