using POS.Shared.Application.Messaging;

namespace Inventory.Application.Catalog.Categories.Commands.UpdateCategory
{
    public sealed record UpdateCategoryCommand(
        Guid Id,
        string NameAr,
        string? NameEn = null,
        Guid? ParentCategoryId = null) : ICommand;
}
