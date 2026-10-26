namespace User.Application;

using Microsoft.Extensions.DependencyInjection;
using User.Application.Services.Auth;
using User.Application.Services.Profile;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
