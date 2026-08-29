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
            builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:7198");

            // Configure as Windows Service
            builder.Host.UseWindowsService(options =>
            {
                options.ServiceName = "POSWebAPI";
            });

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

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers();

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
            await IdentityDataSeeder.SeedAsync(app.Services);
            await SettingsDataSeeder.SeedAsync(app.Services);
            await AuditDataSeeder.SeedAsync(app.Services);
            await InventoryDataSeeder.SeedAsync(app.Services);

            // Enable Swagger UI
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "POS Cashier System API v1");
                c.RoutePrefix = "swagger";
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();
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
        }
    }
}
