using POS.Shared.Application.Messaging;

namespace Purchases.Application.Suppliers.Commands.UpdateSupplier
{
    public sealed record UpdateSupplierCommand(
        Guid Id,
        string Name,
        string Phone,
        string? Email = null,
        string? Address = null,
        string? ContactPerson = null) : ICommand;
}
