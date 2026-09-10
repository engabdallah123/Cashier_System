using POS.Shared.Domain;

namespace Inventory.Domain.Catalog.Products.Errors
{
    public static class ProductErrors
    {
        public static Error NotFound(Guid id) =>
            new("Product.NotFound", $"المنتج بالرقم '{id}' غير موجود.");

        public static Error NotFoundByBarcode(string barcode) =>
            new("Product.NotFoundByBarcode", $"لا يوجد منتج بالباركود '{barcode}'.");

        public static readonly Error InsufficientStock =
            new("Product.InsufficientStock", "الكمية المتاحة في المخزون غير كافية.");

        public static readonly Error DuplicateBarcode =
            Error.Conflict("Product.DuplicateBarcode", "يوجد بالفعل منتج بنفس الباركود.");

        public static readonly Error BarcodeRequired =
            new("Product.BarcodeRequired", "الباركود مطلوب.");

        public static readonly Error NameArRequired =
            new("Product.NameArRequired", "اسم المنتج بالعربية مطلوب.");

        public static readonly Error NameEnRequired =
            new("Product.NameEnRequired", "اسم المنتج بالإنجليزية مطلوب.");

        public static readonly Error NameRequired =
            new("Product.NameRequired", "يجب إدخال اسم المنتج باللغة العربية أو الإنجليزية على الأقل.");

        public static readonly Error InvalidPurchasePrice =
            new("Product.InvalidPurchasePrice", "سعر الشراء لا يمكن أن يكون سالباً.");

        public static readonly Error InvalidSellingPrice =
            new("Product.InvalidSellingPrice", "سعر البيع لا يمكن أن يكون سالباً.");
    }
}
