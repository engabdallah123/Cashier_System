using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sales.Domain;
using Sales.Infrastructre.Database;

namespace Sales.Infrastructre
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSalesInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration));

            services.AddDbContext<SalesDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            services.AddScoped<ISalesUnitOfWork, SalesUnitOfWork>();

            return services;
        }
    }
}
