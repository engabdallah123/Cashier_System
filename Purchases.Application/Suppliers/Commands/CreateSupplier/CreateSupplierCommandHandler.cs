using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Purchases.Domain;
using Purchases.Domain.Suppliers.Entities;

namespace Purchases.Application.Suppliers.Commands.CreateSupplier
{
    internal sealed class CreateSupplierCommandHandler : ICommandHandler<CreateSupplierCommand, Guid>
    {
        private readonly IPurchasesUnitOfWork _unitOfWork;

        public CreateSupplierCommandHandler(IPurchasesUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
        {
            var supplierResult = Supplier.Create(
                request.Name, request.Phone, request.Email,
                request.Address, request.ContactPerson, request.Id);

            if (supplierResult.IsFailure)
                return Result<Guid>.Failure(supplierResult.Error);

            var supplier = supplierResult.Value!;
            await _unitOfWork.SupplierRepository.AddAsync(supplier);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(supplier.Id);
        }
    }
}
