using POS.Shared.Application.Messaging;

namespace Purchases.Application.Suppliers.Commands.CreateSupplier
{
    public sealed record CreateSupplierCommand(
        string Name,
        string Phone,
        string? Email = null,
        string? Address = null,
        string? ContactPerson = null,
        Guid? Id = null) : ICommand<Guid>;
}
