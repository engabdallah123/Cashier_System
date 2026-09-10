using POS.Shared.Application.Messaging;

namespace Dashboard.Application.Dashboard.Queries.GetDaySalesDetails
{
    public sealed record GetDaySalesDetailsQuery(
        DateTime Date,
        string? PaymentMethod = null,
        Guid? CashierId = null) : IQuery<DaySalesDetailsResponse>;
}
