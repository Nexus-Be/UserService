namespace User.Application;

using Microsoft.Extensions.DependencyInjection;
using User.Application.Services.Auth;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
