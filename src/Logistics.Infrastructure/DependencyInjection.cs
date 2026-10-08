using Logistics.Application.Common;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Seeding;
using Logistics.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved per context (not read now) so later configuration sources, e.g. test overrides, win.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string 'Default' is missing. Set ConnectionStrings__Default.");

            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5);
                // Shipments load several collections; split queries avoid a cartesian explosion.
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IReferenceGenerator, SqlReferenceGenerator>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));
        services.AddScoped<DataSeeder>();

        return services;
    }
}
