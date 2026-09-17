using POS.Shared.Domain;

namespace Purchases.Domain.Suppliers.Entities
{
    public sealed class Supplier : Entity
    {
        public string Name { get; private set; } = default!;
        public string Phone { get; private set; } = default!;
        public string? Email { get; private set; }
        public string? Address { get; private set; }
        public string? ContactPerson { get; private set; }
        public bool IsActive { get; private set; }

        private Supplier() { } // EF Core

        private Supplier(Guid id, string name, string phone, string? email, string? address, string? contactPerson)
            : base(id)
        {
            Name = name;
            Phone = phone;
            Email = email;
            Address = address;
            ContactPerson = contactPerson;
            IsActive = true;
        }

        public static Result<Supplier> Create(string name, string phone, string? email = null, string? address = null, string? contactPerson = null, Guid? id = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<Supplier>.Failure(SupplierErrors.NameRequired);

            if (string.IsNullOrWhiteSpace(phone))
                return Result<Supplier>.Failure(SupplierErrors.PhoneRequired);

            var supplier = new Supplier(id ?? Guid.NewGuid(), name.Trim(), phone.Trim(), email?.Trim(), address?.Trim(), contactPerson?.Trim());
            return Result<Supplier>.Success(supplier);
        }

        public Result Update(string name, string phone, string? email, string? address, string? contactPerson)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(SupplierErrors.NameRequired);

            if (string.IsNullOrWhiteSpace(phone))
                return Result.Failure(SupplierErrors.PhoneRequired);

            Name = name.Trim();
            Phone = phone.Trim();
            Email = email?.Trim();
            Address = address?.Trim();
            ContactPerson = contactPerson?.Trim();
            return Result.Success();
        }

        public Result Activate() { IsActive = true; return Result.Success(); }
        public Result Deactivate() { IsActive = false; return Result.Success(); }
    }
}
