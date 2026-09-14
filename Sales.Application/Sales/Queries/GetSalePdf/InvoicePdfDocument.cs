using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sales.Application.Sales.Queries;

namespace Sales.Application.Sales.Queries.GetSalePdf
{
    public class InvoicePdfDocument : IDocument
    {
        private static readonly CultureInfo ArabicCulture = new("ar-EG");
        private readonly ReceiptResponse _receipt;
        private readonly bool _isThermal;

        public InvoicePdfDocument(ReceiptResponse receipt, bool isThermal = false)
        {
            _receipt = receipt;
            _isThermal = isThermal;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            if (_isThermal)
            {
                ComposeThermal(container);
            }
            else
            {
                ComposeA4(container);
            }
        }

        private void ComposeThermal(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.ContinuousSize(80, Unit.Millimetre);
                page.Margin(3, Unit.Millimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Cairo", "Segoe UI", "Tahoma"));

                page.Content().Column(column =>
                {
                    // 1. Brand Logo (takes full width of the receipt)
                    if (_receipt.LogoBytes != null && _receipt.LogoBytes.Length > 0)
                    {
                        column.Item().PaddingBottom(3).Image(_receipt.LogoBytes).FitWidth();
                    }
                    else if (!string.IsNullOrWhiteSpace(_receipt.StoreName))
                    {
                        // 2. Store / Branch Name (fallback when logo is absent)
                        column.Item().AlignCenter().Text(_receipt.StoreName).FontSize(12).Bold();
                    }

                    // 3. Address
                    if (!string.IsNullOrWhiteSpace(_receipt.Address))
                        column.Item().AlignCenter().Text(_receipt.Address).FontSize(8.5f).FontColor(Colors.Grey.Darken3);

                    // 4. Phone / Hotline
                    if (!string.IsNullOrWhiteSpace(_receipt.Phone))
                        column.Item().AlignCenter().Text(_receipt.Phone).FontSize(8.5f).FontColor(Colors.Grey.Darken3);

                    // 5. Boxed Order Number
                    int orderNum = _receipt.OrderNumber > 0 ? _receipt.OrderNumber : 1;
                    column.Item().PaddingTop(4).AlignCenter().Border(1.5f).BorderColor(Colors.Black).PaddingVertical(3).PaddingHorizontal(16)
                        .Text(FormatRtl($"فاتورة # {orderNum}")).FontSize(14).ExtraBold();

                    // 6. Print Time
                    var now = DateTime.Now;
                    string nowPeriod = now.Hour >= 12 ? "م" : "ص";
                    column.Item().PaddingTop(3).AlignCenter().Text(t =>
                    {
                        t.Span(FormatRtl($"وقت الطباعة: {now:dd/MM/yyyy}  \u202A{now:hh:mm:ss}\u202C {nowPeriod}")).FontSize(7.5f);
                    });

                    // 7. Divider Line
                    column.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // 8. Invoice Details
                    string shortInvNum = ExtractShortInvoiceNumber(_receipt.InvoiceNumber, orderNum);
                    string? orderType = !string.IsNullOrWhiteSpace(_receipt.OrderType) && _receipt.OrderType != "POS Desktop Sale"
                        ? _receipt.OrderType
                        : null;

                    column.Item().Row(r =>
                    {
                        if (!string.IsNullOrWhiteSpace(orderType))
                        {
                            r.RelativeItem().AlignLeft().Text(FormatRtl(orderType)).FontSize(8).Bold();
                        }
                        else
                        {
                            r.RelativeItem();
                        }
                        r.RelativeItem().AlignRight().Text(FormatRtl($"فاتورة # {shortInvNum}")).FontSize(8).Bold();
                    });

                    var saleDate = _receipt.SaleDate.ToLocalTime();
                    string salePeriod = saleDate.Hour >= 12 ? "م" : "ص";
                    column.Item().AlignRight().Text(t =>
                    {
                        t.Span(FormatRtl($"التاريخ: {saleDate:dd/MM/yyyy}  \u202A{saleDate:hh:mm:ss}\u202C {salePeriod}")).FontSize(7.5f);
                    });

                    string cashier = string.IsNullOrWhiteSpace(_receipt.CashierName) ? "الكاشير" : _receipt.CashierName;
                    column.Item().Row(r =>
                    {
                        r.RelativeItem().AlignLeft().Text(FormatRtl($"المنشئ: {cashier}")).FontSize(7.5f);
                        r.RelativeItem().AlignRight().Text(FormatRtl($"المغلق: {cashier}")).FontSize(7.5f);
                    });

                    // 9. Divider Line
                    column.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // 10. Items Table
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);   // الكمية والوحدة
                            columns.RelativeColumn(1);   // المنتج ونوع البيع
                            columns.ConstantColumn(64);  // السعر الإجمالي
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(0.5f).BorderColor(Colors.Black).PaddingBottom(2).AlignLeft().Text("الكمية").FontSize(7.5f).Bold();
                            header.Cell().BorderBottom(0.5f).BorderColor(Colors.Black).PaddingBottom(2).AlignCenter().Text("المنتج").FontSize(7.5f).Bold();
                            header.Cell().BorderBottom(0.5f).BorderColor(Colors.Black).PaddingBottom(2).AlignRight().Text("السعر").FontSize(7.5f).Bold();
                        });

                        for (int i = 0; i < _receipt.Items.Count; i++)
                        {
                            var item = _receipt.Items[i];
                            bool isLast = i == _receipt.Items.Count - 1;
                            string unitLabel = !string.IsNullOrWhiteSpace(item.UnitName) ? item.UnitName : "قطعة";

                            var cellStyle = (IContainer cell) =>
                            {
                                var c = cell;
                                if (!isLast)
                                {
                                    c = c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                                }
                                return c.PaddingTop(3.5f).PaddingBottom(3.5f);
                            };

                            cellStyle(table.Cell()).AlignLeft().Text(FormatRtl($"{item.Quantity:G29} {unitLabel}")).FontSize(7.5f).SemiBold();

                            cellStyle(table.Cell()).AlignRight().Column(col =>
                            {
                                string typeBadge = !string.IsNullOrEmpty(item.PriceType) ? $" [{item.PriceType}]" : "";
                                col.Item().Text(FormatRtl($"{item.ProductName ?? "صنف"}{typeBadge}")).FontSize(8).Bold();
                                if (!string.IsNullOrWhiteSpace(item.PackagingInfo) && item.PackagingInfo.Contains("كرتونة"))
                                {
                                    col.Item().Text(FormatRtl($"التعبئة: {item.PackagingInfo}")).FontSize(6.8f).FontColor(Colors.Grey.Darken2);
                                }
                            });

                            cellStyle(table.Cell()).AlignRight().Text($"{item.Total:N2} {_receipt.Currency}").FontSize(8).SemiBold();
                        }
                    });

                    // 11. Divider Line
                    column.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Black);

                    // 12. Totals & Payment
                    column.Item().Row(r =>
                    {
                        r.RelativeItem().AlignRight().Text("الإجمالي").FontSize(9.5f).Bold();
                        r.ConstantItem(75).AlignRight().Text($"{_receipt.TotalAmount:N2} {_receipt.Currency}").FontSize(9.5f).Bold();
                    });

                    if (_receipt.DiscountAmount > 0)
                    {
                        column.Item().Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("الخصم").FontSize(8);
                            r.ConstantItem(75).AlignRight().Text($"-{_receipt.DiscountAmount:N2} {_receipt.Currency}").FontSize(8);
                        });
                    }

                    if (_receipt.TaxAmount > 0)
                    {
                        column.Item().Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("الضريبة").FontSize(8);
                            r.ConstantItem(75).AlignRight().Text($"+{_receipt.TaxAmount:N2} {_receipt.Currency}").FontSize(8);
                        });
                    }

                    column.Item().Row(r =>
                    {
                        r.RelativeItem().AlignRight().Text(FormatRtl($"الدفع - {_receipt.PaymentMethod}")).FontSize(8.5f).SemiBold();
                        r.ConstantItem(75).AlignRight().Text($"{(_receipt.PaidAmount > 0 ? _receipt.PaidAmount : _receipt.TotalAmount):N2} {_receipt.Currency}").FontSize(8.5f).SemiBold();
                    });

                    if (_receipt.ChangeAmount > 0)
                    {
                        column.Item().Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("الباقي").FontSize(8);
                            r.ConstantItem(75).AlignRight().Text($"{_receipt.ChangeAmount:N2} {_receipt.Currency}").FontSize(8);
                        });
                    }

                    var totalQty = _receipt.Items.Count;
                    column.Item().PaddingTop(5).PaddingBottom(3).AlignCenter().Layers(layers =>
                    {
                        layers.Layer().Svg("<svg viewBox='0 0 100 30' preserveAspectRatio='none' xmlns='http://www.w3.org/2000/svg'><rect x='1' y='1' width='98' height='28' rx='14' ry='14' fill='none' stroke='black' stroke-width='2'/></svg>");

                        layers.PrimaryLayer().PaddingVertical(3).PaddingHorizontal(14).Text(t =>
                        {
                            t.Span(FormatRtl("إجمالي عدد المنتجات: ")).FontSize(9).Bold();
                            t.Span($"{totalQty}").FontSize(10.5f).ExtraBold();
                        });
                    });

                    // 13. Footer Line & Message
                    column.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Black);

                    if (!string.IsNullOrWhiteSpace(_receipt.InvoiceFooterMessage))
                    {
                        column.Item().AlignCenter().Text(FormatRtl(_receipt.InvoiceFooterMessage)).FontSize(8).SemiBold();
                    }
                    else
                    {
                        column.Item().AlignCenter().Text(FormatRtl("شكراً لزيارتكم!")).FontSize(8).SemiBold();
                    }

                    // 14. QR Code for Fast Scanner Lookup
                    if (!string.IsNullOrWhiteSpace(_receipt.InvoiceNumber))
                    {
                        column.Item().PaddingTop(4).AlignCenter().Column(barcodeCol =>
                        {
                            var qrBytes = BarcodeAndQrHelper.GenerateQrCodePng(_receipt.InvoiceNumber, 8);
                            if (qrBytes != null && qrBytes.Length > 0)
                            {
                                barcodeCol.Item().AlignCenter().Width(62).Height(62).Image(qrBytes).FitArea();
                            }

                            barcodeCol.Item().PaddingTop(2).AlignCenter().Text(_receipt.InvoiceNumber).FontSize(8).Bold();
                        });
                    }
                });
            });
        }

        private static string FormatRtl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return $"\u202B\u200F{text}\u200F\u202C";
        }

        private static string ExtractShortInvoiceNumber(string invoiceNumber, int orderNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber)) return orderNumber.ToString();
            var parts = invoiceNumber.Split('-');
            if (parts.Length == 3)
            {
                return parts[^1];
            }
            return invoiceNumber.Replace("INV-", "");
        }

        private void ComposeA4(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Cairo", "Segoe UI", "Arial"));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
        }

        private void ComposeHeader(IContainer container)
        {
            var titleStyle = TextStyle.Default.FontSize(20).Bold().FontFamily("Cairo", "Segoe UI", "Arial").FontColor(Colors.Blue.Darken3);

            container.Row(row =>
            {
                if (_receipt.LogoBytes != null && _receipt.LogoBytes.Length > 0)
                {
                    row.ConstantItem(70).MaxHeight(60).PaddingRight(10).Image(_receipt.LogoBytes).FitArea();
                }

                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(_receipt.StoreName).Style(titleStyle);
                    if (!string.IsNullOrWhiteSpace(_receipt.Address))
                        column.Item().Text(_receipt.Address).FontSize(9).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrWhiteSpace(_receipt.Phone))
                        column.Item().Text($"Tel: {_receipt.Phone}").FontSize(9).FontColor(Colors.Grey.Medium);
                });

                row.ConstantItem(240).Row(headerRight =>
                {
                    var qrBytes = BarcodeAndQrHelper.GenerateQrCodePng(_receipt.InvoiceNumber, 6);
                    if (qrBytes != null && qrBytes.Length > 0)
                    {
                        headerRight.ConstantItem(52).Height(52).Image(qrBytes).FitArea();
                    }

                    headerRight.RelativeItem().PaddingLeft(6).Column(column =>
                    {
                        column.Item().AlignRight().Text("SALES INVOICE").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                        column.Item().AlignRight().Text($"Invoice #: {_receipt.InvoiceNumber}").FontSize(9).SemiBold();
                        column.Item().AlignRight().Text($"Date: {_receipt.SaleDate:dd/MM/yyyy HH:mm}").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingVertical(15).Column(column =>
            {
                // Customer & Cashier Details
                column.Item().Row(row =>
                {
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("CUSTOMER DETAILS").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                        c.Item().Text(_receipt.CustomerName ?? "Walk-in Customer").FontSize(10).Bold();
                    });

                    row.ConstantItem(15);

                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("SALE DETAILS").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"Cashier: {_receipt.CashierName}").FontSize(9);
                        c.Item().Text($"Payment Method: {_receipt.PaymentMethod}").FontSize(9);
                    });
                });

                column.Item().Height(15);

                // Items Table
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(25);  // #
                        columns.RelativeColumn(4);   // Item
                        columns.RelativeColumn(2);   // Qty & Packaging
                        columns.RelativeColumn(2);   // Price
                        columns.RelativeColumn(2);   // Total
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCellStyle).Text("#");
                        header.Cell().Element(HeaderCellStyle).Text("Item Description");
                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("Qty / Unit");
                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("Unit Price");
                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total");

                        static IContainer HeaderCellStyle(IContainer c) =>
                            c.Background(Colors.Blue.Darken3).PaddingVertical(5).PaddingHorizontal(8).DefaultTextStyle(x => x.Bold().FontColor(Colors.White).FontSize(9));
                    });

                    int index = 1;
                    foreach (var item in _receipt.Items)
                    {
                        var bg = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                        table.Cell().Element(c => CellStyle(c, bg)).Text(index.ToString());
                        table.Cell().Element(c => CellStyle(c, bg)).Column(col =>
                        {
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text(item.ProductName ?? "Item").Bold();
                                if (!string.IsNullOrWhiteSpace(item.PriceType))
                                {
                                    r.AutoItem().Text($" [{item.PriceType}]").FontSize(8.5f).Bold().FontColor(item.PriceType == "جملة" ? Colors.Orange.Darken2 : Colors.Blue.Darken2);
                                }
                            });
                            if (!string.IsNullOrWhiteSpace(item.Barcode))
                                col.Item().Text($"Barcode: {item.Barcode}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });

                        string qtyDisplay = !string.IsNullOrWhiteSpace(item.PackagingInfo) ? item.PackagingInfo : $"{item.Quantity:N2} {item.UnitName ?? "قطعة"}";
                        table.Cell().Element(c => CellStyle(c, bg)).AlignRight().Text(qtyDisplay);
                        table.Cell().Element(c => CellStyle(c, bg)).AlignRight().Text($"{item.UnitPrice:N2} {_receipt.Currency}");
                        table.Cell().Element(c => CellStyle(c, bg)).AlignRight().Text($"{item.Total:N2} {_receipt.Currency}").Bold();

                        index++;
                    }

                    static IContainer CellStyle(IContainer c, string backgroundColor) =>
                        c.Background(backgroundColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(6).PaddingHorizontal(8).DefaultTextStyle(x => x.FontSize(9));
                });

                column.Item().Height(15);

                // Totals Summary Box
                column.Item().Row(row =>
                {
                    row.RelativeItem(); // Left spacer

                    row.ConstantItem(250).Column(c =>
                    {
                        c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                        {
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Subtotal:");
                                r.ConstantItem(100).AlignRight().Text($"{_receipt.SubTotal:N2} {_receipt.Currency}");
                            });

                            if (_receipt.DiscountAmount > 0)
                            {
                                col.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Discount:").FontColor(Colors.Red.Medium);
                                    r.ConstantItem(100).AlignRight().Text($"-{_receipt.DiscountAmount:N2} {_receipt.Currency}").FontColor(Colors.Red.Medium);
                                });
                            }

                            if (_receipt.TaxAmount > 0)
                            {
                                col.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Tax:");
                                    r.ConstantItem(100).AlignRight().Text($"+{_receipt.TaxAmount:N2} {_receipt.Currency}");
                                });
                            }

                            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Grand Total:").FontSize(11).Bold();
                                r.ConstantItem(100).AlignRight().Text($"{_receipt.TotalAmount:N2} {_receipt.Currency}").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                            });

                            col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Paid Amount:").FontSize(9);
                                r.ConstantItem(100).AlignRight().Text($"{_receipt.PaidAmount:N2} {_receipt.Currency}").FontSize(9);
                            });

                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Change Due:").FontSize(9);
                                r.ConstantItem(100).AlignRight().Text($"{_receipt.ChangeAmount:N2} {_receipt.Currency}").FontSize(9);
                            });
                        });
                    });
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.Column(column =>
            {

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                column.Item().PaddingTop(5).Row(row =>
                {
                    if (!string.IsNullOrWhiteSpace(_receipt.InvoiceFooterMessage))
                    {
                        row.RelativeItem().Text(_receipt.InvoiceFooterMessage).FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        row.RelativeItem().Text("Thank you for your business!").FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                    }

                    row.ConstantItem(120).AlignRight().Text(x =>
                    {
                        x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }
    }
}
