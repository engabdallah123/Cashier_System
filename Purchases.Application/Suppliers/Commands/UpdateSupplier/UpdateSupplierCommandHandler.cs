using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Purchases.Domain;
using Purchases.Domain.Suppliers;

namespace Purchases.Application.Suppliers.Commands.UpdateSupplier
{
    internal sealed class UpdateSupplierCommandHandler : ICommandHandler<UpdateSupplierCommand>
    {
        private readonly IPurchasesUnitOfWork _unitOfWork;

        public UpdateSupplierCommandHandler(IPurchasesUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
        {
            var supplier = await _unitOfWork.SupplierRepository.GetByIdAsync(request.Id);
            if (supplier is null)
                return Result.Failure(SupplierErrors.NotFound(request.Id));

            var updateResult = supplier.Update(
                request.Name,
                request.Phone,
                request.Email,
                request.Address,
                request.ContactPerson);

            if (updateResult.IsFailure)
                return updateResult;

            _unitOfWork.SupplierRepository.Update(supplier);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
