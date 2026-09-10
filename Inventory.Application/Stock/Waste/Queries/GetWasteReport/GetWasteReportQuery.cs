using POS.Shared.Application.Messaging;

namespace Inventory.Application.Stock.Waste.Queries.GetWasteReport
{
    public sealed record GetWasteReportQuery(
        Guid? ProductId = null,
        string? Reason = null,
        string? Source = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null) : IQuery<WasteReportResponse>;
}
