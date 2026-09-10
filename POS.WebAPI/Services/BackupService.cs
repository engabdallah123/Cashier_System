using Dapper;
using Microsoft.Extensions.Logging;
using POS.Shared.Application.Database;
using System.Data;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace POS.WebAPI.Services
{
    public class BackupService : IBackupService
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ILogger<BackupService> _logger;

        private static readonly string[] TablesToDeleteInOrder = new[]
        {
            "[Returns].[PurchaseReturnItems]",
            "[Returns].[PurchaseReturns]",
            "[Returns].[SalesReturnItems]",
            "[Returns].[SalesReturns]",
            "[Purchases].[PurchaseItems]",
            "[Purchases].[Purchases]",
            "[Sales].[SaleItems]",
            "[Sales].[Sales]",
            "[Inventory].[StockMovements]",
            "[Expenses].[Expenses]",
            "[Shifts].[Shifts]",
            "[Purchases].[Suppliers]",
            "[Sales].[Customers]",
            "[Inventory].[Products]",
            "[Inventory].[Categories]",
            "[Inventory].[Units]",
            "[Settings].[StoreSettings]",
            "[Audit].[AuditLogs]"
        };

        private static readonly (string Schema, string Table, string PropName)[] TablesToProcessInOrder = new[]
        {
            ("Settings", "StoreSettings", "StoreSettings"),
            ("Inventory", "Units", "Units"),
            ("Inventory", "Categories", "Categories"),
            ("Inventory", "Products", "Products"),
            ("Inventory", "StockMovements", "StockMovements"),
            ("Sales", "Customers", "Customers"),
            ("Purchases", "Suppliers", "Suppliers"),
            ("Shifts", "Shifts", "Shifts"),
            ("Expenses", "Expenses", "Expenses"),
            ("Sales", "Sales", "Sales"),
            ("Sales", "SaleItems", "SaleItems"),
            ("Purchases", "Purchases", "Purchases"),
            ("Purchases", "PurchaseItems", "PurchaseItems"),
            ("Returns", "SalesReturns", "SalesReturns"),
            ("Returns", "SalesReturnItems", "SalesReturnItems"),
            ("Returns", "PurchaseReturns", "PurchaseReturns"),
            ("Returns", "PurchaseReturnItems", "PurchaseReturnItems"),
            ("Audit", "AuditLogs", "AuditLogs")
        };

        public BackupService(
            ISqlConnectionFactory sqlConnectionFactory,
            ILogger<BackupService> logger)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _logger = logger;
        }

        public async Task<(byte[] FileBytes, string FileName)> ExportBackupAsync(CancellationToken ct = default)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var backupDict = new Dictionary<string, object>
            {
                ["ExportedAt"] = DateTime.UtcNow,
                ["Version"] = "1.0",
                ["SystemName"] = "POS Cashier System"
            };

            int totalRows = 0;
            foreach (var (schema, table, propName) in TablesToProcessInOrder)
            {
                var tableData = await QueryTableDataAsync(connection, schema, table, ct);
                backupDict[propName] = tableData;
                totalRows += tableData.Count;
            }

            backupDict["TotalRecords"] = totalRows;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(backupDict, options);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var fileName = $"POS_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";

            _logger.LogInformation("Backup export completed with {TotalRows} records across all tables.", totalRows);

            return (bytes, fileName);
        }

        public async Task<(bool Success, string Message, int TotalRows, Dictionary<string, int> RowsPerTable)> RestoreBackupAsync(
            JsonElement rootElement,
            CancellationToken ct = default)
        {
            if (rootElement.ValueKind != JsonValueKind.Object)
            {
                return (false, "ملف النسخة الاحتياطية غير صالح أو فارغ.", 0, new Dictionary<string, int>());
            }

            // 1. Pre-validation: scan JSON and check for non-empty objects with properties
            int validRowsFound = 0;
            var tableElementsToRestore = new List<(string Schema, string Table, string PropName, JsonElement ArrayEl)>();

            foreach (var (schema, table, propName) in TablesToProcessInOrder)
            {
                if (TryGetTableElement(rootElement, propName, out var arrayEl) && arrayEl.ValueKind == JsonValueKind.Array)
                {
                    int rowCountInArray = 0;
                    foreach (var row in arrayEl.EnumerateArray())
                    {
                        if (row.ValueKind == JsonValueKind.Object && row.EnumerateObject().Any())
                        {
                            rowCountInArray++;
                        }
                    }

                    if (rowCountInArray > 0)
                    {
                        validRowsFound += rowCountInArray;
                        tableElementsToRestore.Add((schema, table, propName, arrayEl));
                    }
                }
            }

            if (validRowsFound == 0)
            {
                _logger.LogWarning("Restore attempted with a file containing 0 valid records or empty objects. Aborting without modifying database.");
                return (false, "الملف المحدد لا يحتوي على أي بيانات صالحة للاسترجاع (قد يكون ملفاً تالفاً أو تم تصديره قديماً ككائنات فارغة). تم إلغاء العملية لحماية بياناتك الحالية دون أي مساس بها.", 0, new Dictionary<string, int>());
            }

            using var connection = _sqlConnectionFactory.CreateConnection();
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using var transaction = connection.BeginTransaction();
            var rowsPerTable = new Dictionary<string, int>();

            try
            {
                // 2. Disable constraints
                foreach (var tbl in TablesToDeleteInOrder)
                {
                    await connection.ExecuteAsync($"ALTER TABLE {tbl} NOCHECK CONSTRAINT ALL;", transaction: transaction);
                }

                // 3. Delete existing records (child first)
                foreach (var tbl in TablesToDeleteInOrder)
                {
                    await connection.ExecuteAsync($"DELETE FROM {tbl};", transaction: transaction);
                }

                // 4. Insert data from backup (parent first)
                int totalRowsRestored = 0;
                foreach (var (schema, table, propName, arrayEl) in tableElementsToRestore)
                {
                    int tableRows = await InsertTableDataAsync(connection, transaction, schema, table, arrayEl);
                    rowsPerTable[propName] = tableRows;
                    totalRowsRestored += tableRows;
                }

                // 5. Re-enable constraints
                foreach (var tbl in TablesToDeleteInOrder)
                {
                    try
                    {
                        await connection.ExecuteAsync($"ALTER TABLE {tbl} WITH CHECK CHECK CONSTRAINT ALL;", transaction: transaction);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not re-enable constraints on table {Table}", tbl);
                    }
                }

                transaction.Commit();

                _logger.LogInformation("Restore completed successfully. Total rows restored: {TotalRows}", totalRowsRestored);

                return (true, $"تم استرجاع النسخة الاحتياطية بنجاح. تم استعادة {totalRowsRestored} سجلاً وتحديث جميع بيانات النظام.", totalRowsRestored, rowsPerTable);
            }
            catch (Exception ex)
            {
                try
                {
                    transaction.Rollback();
                }
                catch { }

                _logger.LogError(ex, "Failed to restore backup.");
                return (false, $"فشلت عملية استرجاع النسخة الاحتياطية: {ex.Message}", 0, rowsPerTable);
            }
        }

        private static async Task<List<Dictionary<string, object?>>> QueryTableDataAsync(
            IDbConnection connection,
            string schema,
            string table,
            CancellationToken ct)
        {
            var list = new List<Dictionary<string, object?>>();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM [{schema}].[{table}]";

            if (command is DbCommand dbCommand)
            {
                using var reader = await dbCommand.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var colName = reader.GetName(i);
                        var value = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                        row[colName] = value;
                    }
                    list.Add(row);
                }
            }
            else
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var colName = reader.GetName(i);
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[colName] = value;
                    }
                    list.Add(row);
                }
            }

            return list;
        }

        private static bool TryGetTableElement(JsonElement root, string tableName, out JsonElement element)
        {
            if (root.TryGetProperty(tableName, out element))
                return true;

            var camel = char.ToLowerInvariant(tableName[0]) + tableName.Substring(1);
            if (root.TryGetProperty(camel, out element))
                return true;

            var lower = tableName.ToLowerInvariant();
            if (root.TryGetProperty(lower, out element))
                return true;

            element = default;
            return false;
        }

        private static async Task<int> InsertTableDataAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string schema,
            string table,
            JsonElement tableArrayElement)
        {
            if (tableArrayElement.ValueKind != JsonValueKind.Array)
                return 0;

            int count = 0;
            foreach (var row in tableArrayElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    continue;

                var parameters = new DynamicParameters();
                var columnNames = new List<string>();
                var paramNames = new List<string>();

                foreach (var prop in row.EnumerateObject())
                {
                    var colName = prop.Name;
                    columnNames.Add($"[{colName}]");
                    paramNames.Add($"@{colName}");

                    object? value = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Null or JsonValueKind.Undefined => null,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number => prop.Value.TryGetInt64(out var l)
                            ? (object)l
                            : prop.Value.GetDecimal(),
                        JsonValueKind.String => prop.Value.TryGetGuid(out var g)
                            ? (object)g
                            : (prop.Value.TryGetDateTime(out var dt) ? (object)dt : prop.Value.GetString()),
                        _ => prop.Value.GetRawText()
                    };

                    parameters.Add(colName, value);
                }

                if (columnNames.Count > 0)
                {
                    var sql = $"INSERT INTO [{schema}].[{table}] ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
                    await connection.ExecuteAsync(sql, parameters, transaction);
                    count++;
                }
            }
            return count;
        }
    }
}
