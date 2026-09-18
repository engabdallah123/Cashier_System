using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Sales.Domain;
using Sales.Domain.Sales;
using Shifts.Domain;

namespace Sales.Application.Sales.Commands.PaySaleInvoice;

internal sealed class PaySaleInvoiceCommandHandler : ICommandHandler<PaySaleInvoiceCommand>
{
    private readonly ISalesUnitOfWork _salesUnitOfWork;
    private readonly IShiftsUnitOfWork _shiftsUnitOfWork;
    private readonly ICacheService _cacheService;

    public PaySaleInvoiceCommandHandler(
        ISalesUnitOfWork salesUnitOfWork,
        IShiftsUnitOfWork shiftsUnitOfWork,
        ICacheService cacheService)
    {
        _salesUnitOfWork = salesUnitOfWork;
        _shiftsUnitOfWork = shiftsUnitOfWork;
        _cacheService = cacheService;
    }

    public async Task<Result> Handle(PaySaleInvoiceCommand request, CancellationToken cancellationToken)
    {
        // تحميل الفاتورة مع الدفعات السابقة لضمان حساب المبلغ المتبقي بشكل صحيح
        var sale = await _salesUnitOfWork.SaleRepository.FindAsync(
            s => s.Id == request.SaleId,
            new[] { "Payments" });
        if (sale is null)
            return Result.Failure(SaleErrors.NotFound(request.SaleId));

        // إذا كان هناك شفت مفتوح مرتبط بهذه الفاتورة أو شفت كاشير نشط، نسجل تحصيل المديونية
        Shifts.Domain.Shifts.Entities.Shift? shift = null;
        try
        {
            shift = await _shiftsUnitOfWork.ShiftRepository.GetByIdAsync(sale.ShiftId, cancellationToken);
            if (shift is null || shift.Status != Shifts.Domain.Shifts.Entities.ShiftStatus.Open)
            {
                shift = await _shiftsUnitOfWork.ShiftRepository.GetActiveShiftByCashierIdAsync(sale.CashierId, cancellationToken);
            }
        }
        catch { }

        Guid? activeShiftId = shift?.Status == Shifts.Domain.Shifts.Entities.ShiftStatus.Open ? shift.Id : null;
        Guid activeCashierId = shift?.Status == Shifts.Domain.Shifts.Entities.ShiftStatus.Open ? shift.CashierId : sale.CashierId;

        var paymentResult = sale.AddPayment(
            request.Amount,
            DateTime.UtcNow,
            "Cash",
            activeCashierId,
            activeShiftId,
            "تحصيل مديونية");

        if (paymentResult.IsFailure)
            return Result.Failure(paymentResult.Error);

        // إضافة الدفعة صراحةً ككيان جديد (Added) لضمان تنفيذ INSERT في قاعدة البيانات بدلاً من التحديث الخاطئ
        await _salesUnitOfWork.SalePaymentRepository.AddAsync(paymentResult.Value!);
        _salesUnitOfWork.SaleRepository.Update(sale);

        // تحديث رصيد العميل إذا كانت الفاتورة مرتبطة بعميل
        if (sale.CustomerId.HasValue && sale.CustomerId.Value != Guid.Empty)
        {
            try
            {
                var customer = await _salesUnitOfWork.CustomerRepository.GetByIdAsync(sale.CustomerId.Value);
                if (customer != null)
                {
                    customer.AdjustBalance(-paymentResult.Value!.Amount);
                    _salesUnitOfWork.CustomerRepository.Update(customer);
                }
            }
            catch { }
        }

        if (shift is not null && shift.Status == Shifts.Domain.Shifts.Entities.ShiftStatus.Open)
        {
            try
            {
                shift.RecordDebtCollection(paymentResult.Value!.Amount, "Cash");
                _shiftsUnitOfWork.ShiftRepository.Update(shift);
                await _shiftsUnitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch { }
        }

        await _salesUnitOfWork.SaveChangesAsync(cancellationToken);

        // مسح كاش لوحة التحكم والتقارير الشهرية لضمان تحديث المؤشرات والربح فورياً
        await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);
        await _cacheService.RemoveByPrefixAsync("monthly_calendar_", cancellationToken);

        return Result.Success();
    }
}
