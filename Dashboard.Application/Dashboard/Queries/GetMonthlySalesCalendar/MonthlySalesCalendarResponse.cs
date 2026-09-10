namespace Dashboard.Application.Dashboard.Queries.GetMonthlySalesCalendar
{
    public sealed record MonthlyDaySalesDto(
        int DayNumber,
        DateTime Date,
        int DayOfWeekIndex, // 0 = السبت, 1 = الأحد, ..., 6 = الجمعة
        string DayNameAr,
        decimal TotalSales,
        int InvoiceCount,
        decimal TotalReturns,
        decimal NetSales,
        decimal TotalPaid,
        decimal TotalExpenses,
        decimal TotalPurchases,
        bool HasSales,
        bool IsToday,
        bool IsWeekend,
        decimal CashSales = 0,
        decimal CreditSales = 0,
        decimal DebtCollections = 0);

    public sealed record MonthlySalesCalendarResponse(
        int Year,
        int Month,
        string MonthNameAr,
        int DaysInMonth,
        int FirstDayDayOfWeek, // 0 = السبت, 1 = الأحد, ..., 6 = الجمعة
        decimal TotalMonthlySales,
        int TotalMonthlyInvoices,
        decimal TotalMonthlyReturns,
        decimal NetMonthlySales,
        decimal TotalMonthlyExpenses,
        decimal TotalMonthlyPurchases,
        decimal DailyAverageSales,
        decimal DailyAverageSalesActiveDays,
        int ActiveDaysCount,
        int HighestSalesDay,
        decimal HighestSalesAmount,
        int LowestSalesDay,
        decimal LowestSalesAmount,
        IReadOnlyList<MonthlyDaySalesDto> Days,
        decimal TotalMonthlyCashSales = 0,
        decimal TotalMonthlyCreditSales = 0,
        decimal TotalMonthlyDebtCollections = 0,
        decimal TotalMonthlyCollected = 0);
}
