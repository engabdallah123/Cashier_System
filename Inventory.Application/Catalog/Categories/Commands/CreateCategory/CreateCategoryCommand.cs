using POS.Shared.Application.Messaging;

namespace Inventory.Application.Catalog.Categories.Commands.CreateCategory
{
    public sealed record CreateCategoryCommand(
        string NameAr,
        string? NameEn = null,
        Guid? ParentCategoryId = null) : ICommand<Guid>;
}
