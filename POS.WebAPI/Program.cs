using Audit.Application;
using Audit.Infrastructre;
using Audit.Infrastructre.Database;
using Dashboard.Application;
using Expenses.Application;
using Expenses.Infrastructre;
using Identity.Application;
using Identity.Infrastructre;
using Identity.Infrastructre.Database;
using Inventory.Application;
using Inventory.Infrastructre;
using Inventory.Infrastructre.Database;
using POS.Shared.Application;
using POS.Shared.Infrastructure;
using POS.WebAPI.Middlewares;
using Purchases.Application;
using Purchases.Infrastructre;
using Returns.Application;
using Returns.Infrastructre;
using Sales.Application;
using Sales.Infrastructre;
using Settings.Application;
using Settings.Infrastructre;
using Settings.Infrastructre.Database;
using Shifts.Application;
using Shifts.Infrastructre;

using Microsoft.EntityFrameworkCore;
using Purchases.Infrastructre.Database;
using Returns.Infrastructre.Database;
using Sales.Infrastructre.Database;
using Expenses.Infrastructre.Database;
using Shifts.Infrastructre.Database;

namespace POS.WebAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Set QuestPDF License to Community
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var builder = WebApplication.CreateBuilder(args);

            // Configure URLs explicitly for production service & desktop app
            builder.WebHost.UseUrls("http://*:5000");

            // Configure as Windows Service
            builder.Host.UseWindowsService(options =>
            {
                options.ServiceName = "POSWebAPI";
            });

            // Dynamically resolve working SQL Server instance (SQLEXPRESS / MSSQLSERVER / LocalDB)
            var resolvedConnectionString = ResolveWorkingConnectionString(builder.Configuration);
            builder.Configuration["ConnectionStrings:DefaultConnection"] = resolvedConnectionString;
            Console.WriteLine($"[Database] Using connection string with DataSource: {new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(resolvedConnectionString).DataSource}");

            // Add Shared Services
            builder.Services.AddSharedApplication();
            builder.Services.AddSharedInfrastructure(builder.Configuration);

            // Add Health Checks (checks SQL connectivity via DbContext)
            builder.Services.AddHealthChecks()
                .AddDbContextCheck<InventoryDbContext>("Database", tags: new[] { "db", "ready" });

            // Add Identity Services
            builder.Services.AddIdentityApplication();
            builder.Services.AddIdentityInfrastructure(builder.Configuration);

            // Add Shifts Services
            builder.Services.AddShiftsApplication();
            builder.Services.AddShiftsInfrastructure(builder.Configuration);

            // Add Inventory Module Services
            builder.Services.AddInventoryApplication();
            builder.Services.AddInventoryInfrastructure(builder.Configuration);

            // Add Purchases Module Services
            builder.Services.AddPurchasesApplication();
            builder.Services.AddPurchasesInfrastructure(builder.Configuration);

            // Add Sales Module Services
            builder.Services.AddSalesApplication();
            builder.Services.AddSalesInfrastructure(builder.Configuration);

            // Add Returns Module Services
            builder.Services.AddReturnsApplication();
            builder.Services.AddReturnsInfrastructure(builder.Configuration);

            // Add Expenses Module Services
            builder.Services.AddExpensesApplication();
            builder.Services.AddExpensesInfrastructure(builder.Configuration);

            // Add Dashboard Module Services
            builder.Services.AddDashboardApplication();

            // Add Settings Module Services
            builder.Services.AddSettingsApplication();
            builder.Services.AddSettingsInfrastructure(builder.Configuration);

            // Add Audit Module Services
            builder.Services.AddAuditApplication();
            builder.Services.AddAuditInfrastructure(builder.Configuration);

            // Add Backup Service & 12-Hour Automatic Database Backup Service
            builder.Services.AddScoped<POS.WebAPI.Services.IBackupService, POS.WebAPI.Services.BackupService>();
            builder.Services.AddHostedService<POS.WebAPI.Services.AutoBackupBackgroundService>();

            // Cloud Synchronization Engine (Syncs Suppliers, Purchases, Stock, Settings, Dashboard with Cloud API)
            builder.Services.AddHostedService<POS.WebAPI.Services.CloudSyncBackgroundService>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers();

            // Support large file uploads and long-running bulk imports
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
                serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(20);
                serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(20);
            });

            // Swagger / OpenAPI Configuration
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    });
            });

            var app = builder.Build();

            // 1. Auto-Apply Database Migrations for all modules
            await ApplyMigrationsAsync(app.Services);

            // 2. Seed Initial Data
            try
            {
                await IdentityDataSeeder.SeedAsync(app.Services);
                await SettingsDataSeeder.SeedAsync(app.Services);
                await AuditDataSeeder.SeedAsync(app.Services);
                await InventoryDataSeeder.SeedAsync(app.Services);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Seed Warning] Database seeding delayed: {ex.Message}");
            }

            // Enable Swagger UI
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "POS Cashier System API v1");
                c.RoutePrefix = "swagger";
            });

            // Note: HttpsRedirection is omitted for local desktop service communication
            app.UseStaticFiles();

            // Serve uploaded files from writable ProgramData location
            try
            {
                var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrWhiteSpace(commonAppData))
                {
                    var uploadsDir = Path.Combine(commonAppData, "POS Cashier System", "uploads");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsDir),
                        RequestPath = "/uploads"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StaticFiles Notice] Uploads directory mapping: {ex.Message}");
            }
            app.UseCors("AllowAll");
            app.UseCustomExceptionHandler();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHealthChecks("/health");

            app.Run();
        }

        private static async Task ApplyMigrationsAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            var dbContexts = new DbContext[]
            {
                sp.GetRequiredService<IdentityModuleDbContext>(),
                sp.GetRequiredService<ShiftsDbContext>(),
                sp.GetRequiredService<InventoryDbContext>(),
                sp.GetRequiredService<PurchasesDbContext>(),
                sp.GetRequiredService<SalesDbContext>(),
                sp.GetRequiredService<ReturnsDbContext>(),
                sp.GetRequiredService<ExpensesDbContext>(),
                sp.GetRequiredService<SettingsDbContext>(),
                sp.GetRequiredService<AuditDbContext>()
            };

            foreach (var context in dbContexts)
            {
                try
                {
                    await context.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Migration Notice] {context.GetType().Name}: {ex.Message}");
                }
            }

            // Self-healing: Repair any sales where TotalAmount was corrupted to 0 but items exist
            try
            {
                var salesContext = sp.GetRequiredService<SalesDbContext>();
                const string repairSalesSql = """
                    UPDATE s
                    SET s.SubTotal = ISNULL(items.TotalItemSum, 0),
                        s.TotalAmount = CASE WHEN (ISNULL(items.TotalItemSum, 0) - s.DiscountAmount + s.TaxAmount) < 0 THEN 0 ELSE (ISNULL(items.TotalItemSum, 0) - s.DiscountAmount + s.TaxAmount) END,
                        s.ChangeAmount = CASE WHEN s.PaidAmount > (ISNULL(items.TotalItemSum, 0) - s.DiscountAmount + s.TaxAmount) 
                                              THEN s.PaidAmount - (ISNULL(items.TotalItemSum, 0) - s.DiscountAmount + s.TaxAmount) 
                                              ELSE 0 END
                    FROM [Sales].[Sales] s
                    CROSS APPLY (
                        SELECT SUM(i.Quantity * i.UnitPrice) AS TotalItemSum
                        FROM [Sales].[SaleItems] i
                        WHERE i.SaleId = s.Id
                    ) items
                    WHERE s.TotalAmount = 0 AND items.TotalItemSum > 0;
                    """;
                await salesContext.Database.ExecuteSqlRawAsync(repairSalesSql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Data Repair Notice] Sales repair check: {ex.Message}");
            }

            // Self-healing: Ensure [Sales].[SalePayments] table exists and backfill initial payments
            try
            {
                var salesContext = sp.GetRequiredService<SalesDbContext>();
                const string ensurePaymentsTableSql = """
                    IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'SalePayments' AND s.name = 'Sales')
                    BEGIN
                        CREATE TABLE [Sales].[SalePayments] (
                            [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                            [SaleId] uniqueidentifier NOT NULL,
                            [Amount] decimal(18,2) NOT NULL,
                            [PaymentDate] datetime2 NOT NULL,
                            [PaymentMethod] nvarchar(50) NOT NULL,
                            [CashierId] uniqueidentifier NOT NULL,
                            [ShiftId] uniqueidentifier NULL,
                            [Notes] nvarchar(500) NULL,
                            CONSTRAINT [FK_SalePayments_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales].[Sales] ([Id]) ON DELETE CASCADE
                        );
                        CREATE INDEX [IX_SalePayments_SaleId] ON [Sales].[SalePayments] ([SaleId]);
                        CREATE INDEX [IX_SalePayments_PaymentDate] ON [Sales].[SalePayments] ([PaymentDate]);
                    END

                    -- Backfill initial payments for historical sales that don't have SalePayments yet
                    INSERT INTO [Sales].[SalePayments] (Id, SaleId, Amount, PaymentDate, PaymentMethod, CashierId, ShiftId, Notes)
                    SELECT 
                        NEWID(), s.Id, s.PaidAmount, s.SaleDate, s.PaymentMethod, s.CashierId, s.ShiftId, N'دفعة أولية عند البيع'
                    FROM [Sales].[Sales] s
                    WHERE s.PaidAmount > 0 
                      AND NOT EXISTS (SELECT 1 FROM [Sales].[SalePayments] p WHERE p.SaleId = s.Id);
                    """;
                await salesContext.Database.ExecuteSqlRawAsync(ensurePaymentsTableSql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Data Repair Notice] SalePayments table setup: {ex.Message}");
            }
        }

        private static string ResolveWorkingConnectionString(IConfiguration configuration)
        {
            var configured = configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrWhiteSpace(configured) && CanConnectToSql(configured))
            {
                return configured;
            }

            var fallbackServers = new[] { ".\\SQLEXPRESS", ".", "localhost", "(localdb)\\MSSQLLocalDB", "127.0.0.1" };
            var connBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configured ?? "Database=POSCashier;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;");

            foreach (var server in fallbackServers)
            {
                connBuilder.DataSource = server;
                connBuilder.ConnectTimeout = 2;
                if (CanConnectToSql(connBuilder.ConnectionString))
                {
                    connBuilder.ConnectTimeout = 30;
                    return connBuilder.ConnectionString;
                }
            }

            return configured ?? "Server=.\\SQLEXPRESS; Database=POSCashier; Integrated Security=True; Encrypt=False; TrustServerCertificate=True; MultipleActiveResultSets=True;";
        }

        private static bool CanConnectToSql(string connectionString)
        {
            try
            {
                var testBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = "master",
                    ConnectTimeout = 2
                };
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(testBuilder.ConnectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
