using System.IO;
using System.Text.Json;
using POS.Desktop.Services.Api;

namespace POS.Desktop.Services.State
{
    public class PurchaseDraftItem
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? BatchNumber { get; set; }
        public string? Unit { get; set; }
        public bool IsWeighable { get; set; } = false;

        public CreatePurchaseItemRequest ToRequest() =>
            new(ProductId, Quantity, UnitCost, Discount, Tax, ExpiryDate, BatchNumber, Unit);

        public static PurchaseDraftItem FromRequest(CreatePurchaseItemRequest req, string productName = "", bool isWeighable = false) =>
            new()
            {
                ProductId = req.ProductId,
                ProductName = productName,
                Quantity = req.Quantity,
                UnitCost = req.UnitCost,
                Discount = req.Discount,
                Tax = req.Tax,
                ExpiryDate = req.ExpiryDate,
                BatchNumber = req.BatchNumber,
                Unit = req.Unit,
                IsWeighable = isWeighable
            };
    }

    public class PurchaseDraftData
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string SupplierId { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public decimal PaidAmount { get; set; }
        public bool IsFullPayment { get; set; } = true;
        public List<PurchaseDraftItem> Items { get; set; } = new();
        public DateTime LastSavedAt { get; set; } = DateTime.Now;
    }

    public class PurchaseDraftStateContainer
    {
        private static readonly string DraftFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "POSCashier",
            "stock_purchase_draft.json");

        public PurchaseDraftData Draft { get; private set; } = new();

        public bool HasDraft => Draft.Items.Count > 0;
        public int ItemsCount => Draft.Items.Count;
        public decimal TotalAmount => Draft.Items.Sum(i => i.Quantity * i.UnitCost);

        public event Action? OnDraftChanged;

        public PurchaseDraftStateContainer()
        {
            LoadFromDisk();
        }

        public void SetDraft(
            string invoiceNumber,
            string supplierId,
            DateTime invoiceDate,
            decimal paidAmount,
            bool isFullPayment,
            IEnumerable<CreatePurchaseItemRequest> items,
            Func<Guid, (string Name, bool IsWeighable)>? getProductInfo = null)
        {
            Draft.InvoiceNumber = invoiceNumber;
            Draft.SupplierId = supplierId;
            Draft.InvoiceDate = invoiceDate;
            Draft.PaidAmount = paidAmount;
            Draft.IsFullPayment = isFullPayment;
            Draft.LastSavedAt = DateTime.Now;

            Draft.Items = items.Select(i =>
            {
                var info = getProductInfo?.Invoke(i.ProductId) ?? ("", false);
                return PurchaseDraftItem.FromRequest(i, info.Name, info.IsWeighable);
            }).ToList();

            SaveToDisk();
            OnDraftChanged?.Invoke();
        }

        public void SetDraft(
            string invoiceNumber,
            string supplierId,
            DateTime invoiceDate,
            decimal paidAmount,
            bool isFullPayment,
            IEnumerable<CreatePurchaseItemRequest> items,
            Func<Guid, string>? getProductName)
        {
            SetDraft(invoiceNumber, supplierId, invoiceDate, paidAmount, isFullPayment, items,
                id => (getProductName?.Invoke(id) ?? "", false));
        }

        public void SaveToDisk()
        {
            try
            {
                if (Draft.Items.Count == 0)
                {
                    ClearDiskFile();
                    return;
                }

                var dir = Path.GetDirectoryName(DraftFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(Draft, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DraftFilePath, json);
            }
            catch
            {
                // Suppress disk write errors
            }
        }

        public void LoadFromDisk()
        {
            try
            {
                if (File.Exists(DraftFilePath))
                {
                    var json = File.ReadAllText(DraftFilePath);
                    var data = JsonSerializer.Deserialize<PurchaseDraftData>(json);
                    if (data != null && data.Items != null && data.Items.Count > 0)
                    {
                        Draft = data;
                        OnDraftChanged?.Invoke();
                        return;
                    }
                }
            }
            catch
            {
                // Fallback to empty draft
            }

            Draft = new PurchaseDraftData();
        }

        public void ClearDraft()
        {
            Draft = new PurchaseDraftData();
            ClearDiskFile();
            OnDraftChanged?.Invoke();
        }

        private void ClearDiskFile()
        {
            try
            {
                if (File.Exists(DraftFilePath))
                {
                    File.Delete(DraftFilePath);
                }
            }
            catch
            {
                // Suppress deletion error
            }
        }
    }
}
