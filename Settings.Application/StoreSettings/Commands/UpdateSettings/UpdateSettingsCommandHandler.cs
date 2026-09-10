using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Settings.Domain;
using Settings.Domain.StoreSettings;
using Settings.Domain.StoreSettings.Entities;

namespace Settings.Application.StoreSettings.Commands.UpdateSettings
{
    internal sealed class UpdateSettingsCommandHandler : ICommandHandler<UpdateSettingsCommand>
    {
        private readonly ISettingsUnitOfWork _unitOfWork;

        public UpdateSettingsCommandHandler(ISettingsUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = (await _unitOfWork.StoreSettingRepository.GetAllAsync())
                .FirstOrDefault();

            if (settings is null)
            {
                // أول مرة — إنشاء إعدادات جديدة
                var createResult = StoreSetting.Create(
                    request.StoreName, request.Currency,
                    request.TaxRate, request.IsTaxIncluded,
                    request.Address, request.Phone,
                    request.InvoiceFooterMessage, request.AllowNegativeStock,
                    request.AutoPrintInvoice, request.LogoUrl);

                if (createResult.IsFailure)
                    return Result.Failure(createResult.Error);

                await _unitOfWork.StoreSettingRepository.AddAsync(createResult.Value!);
            }
            else
            {
                var updateResult = settings.Update(
                    request.StoreName, request.Address, request.Phone,
                    request.TaxRate, request.IsTaxIncluded, request.Currency,
                    request.InvoiceFooterMessage, request.AllowNegativeStock,
                    request.AutoPrintInvoice, request.LogoUrl);

                if (updateResult.IsFailure)
                    return updateResult;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
