using POS.Shared.Domain;

namespace Inventory.Domain.Catalog.Categories
{
    public sealed class Category : Entity
    {
        public string NameAr { get; private set; } = default!;
        public string NameEn { get; private set; } = default!;
        public Guid? ParentCategoryId { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private Category() { } // EF Core

        private Category(Guid id, string nameAr, string nameEn, Guid? parentCategoryId)
            : base(id)
        {
            NameAr = nameAr;
            NameEn = nameEn;
            ParentCategoryId = parentCategoryId;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }

        public static Result<Category> Create(string nameAr, string? nameEn = null, Guid? parentCategoryId = null, Guid? id = null)
        {
            if (string.IsNullOrWhiteSpace(nameAr))
                return Result<Category>.Failure(CategoryErrors.NameArRequired);

            var en = string.IsNullOrWhiteSpace(nameEn) ? nameAr.Trim() : nameEn.Trim();

            var categoryId = id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.NewGuid();
            var category = new Category(categoryId, nameAr.Trim(), en, parentCategoryId);
            return Result<Category>.Success(category);
        }

        public Result Update(string nameAr, string? nameEn = null, Guid? parentCategoryId = null)
        {
            if (string.IsNullOrWhiteSpace(nameAr))
                return Result.Failure(CategoryErrors.NameArRequired);

            var en = string.IsNullOrWhiteSpace(nameEn) ? nameAr.Trim() : nameEn.Trim();

            NameAr = nameAr.Trim();
            NameEn = en;
            ParentCategoryId = parentCategoryId;
            return Result.Success();
        }

        public Result Activate() { IsActive = true; return Result.Success(); }
        public Result Deactivate() { IsActive = false; return Result.Success(); }
    }
}
