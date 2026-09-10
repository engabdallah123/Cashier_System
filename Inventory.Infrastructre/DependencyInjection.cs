using Inventory.Domain;
using Inventory.Infrastructre.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructre
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInventoryInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration));

            services.AddDbContext<InventoryDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.CommandTimeout(300);
                }));

            services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();

            return services;
        }
    }
}
