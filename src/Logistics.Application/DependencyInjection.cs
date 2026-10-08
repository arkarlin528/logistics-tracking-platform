using FluentValidation;
using Logistics.Application.Auth;
using Logistics.Application.Dashboard;
using Logistics.Application.Rates;
using Logistics.Application.Reference;
using Logistics.Application.Shipments;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ShipmentQueries>();
        services.AddScoped<ShipmentCommands>();
        services.AddScoped<ReferenceDataQueries>();
        services.AddScoped<RateService>();
        services.AddScoped<DashboardQueries>();
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
