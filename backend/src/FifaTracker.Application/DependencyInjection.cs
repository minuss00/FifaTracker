using FifaTracker.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FifaTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IMatchGenerator, MatchGenerator>();
        services.AddScoped<IMatchCombinationCache, MatchCombinationCache>();

        return services;
    }
}
