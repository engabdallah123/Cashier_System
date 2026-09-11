using POS.Shared.Application.Messaging;
using Sales.Application.Sales.Queries.GetSaleById;

namespace Sales.Application.Sales.Queries.GetSaleByInvoiceNumber
{
    public sealed record GetSaleByInvoiceNumberQuery(string InvoiceNumber) : IQuery<SaleDetailResponse>;
}
