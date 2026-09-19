using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Settings.Domain.StoreSettings;

namespace Settings.Application.StoreSettings.Queries.GetSettings
{
    internal sealed class GetSettingsQueryHandler : IQueryHandler<GetSettingsQuery, StoreSettingResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetSettingsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<StoreSettingResponse>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            const string sql = """
                SELECT TOP 1
                    Id,
                    StoreName,
                    Address,
                    Phone,
                    TaxRate,
                    IsTaxIncluded,
                    Currency,
                    InvoiceFooterMessage,
                    AllowNegativeStock,
                    ISNULL(AutoPrintInvoice, 1) AS AutoPrintInvoice,
                    ISNULL(EnableDepletedBatchAlert, 1) AS EnableDepletedBatchAlert,
                    LogoUrl,
                    UpdatedAt
                FROM [Settings].[StoreSettings]
                """;

            var setting = await connection.QuerySingleOrDefaultAsync<StoreSettingResponse>(sql);

            if (setting is null)
                return Result<StoreSettingResponse>.Failure(StoreSettingErrors.NotFound);

            return Result<StoreSettingResponse>.Success(setting);
        }
    }
}
