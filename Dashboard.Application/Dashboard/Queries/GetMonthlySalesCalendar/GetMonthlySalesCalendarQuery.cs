using POS.Shared.Application.Messaging;

namespace Dashboard.Application.Dashboard.Queries.GetMonthlySalesCalendar
{
    public sealed record GetMonthlySalesCalendarQuery(
        int Year,
        int Month,
        string? PaymentMethod = null,
        Guid? CashierId = null) : IQuery<MonthlySalesCalendarResponse>;
}
