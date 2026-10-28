namespace User.Application;

using Microsoft.Extensions.DependencyInjection;
using User.Application.Services.Admin;
using User.Application.Services.Auth;
using User.Application.Services.Profile;
using User.Application.Services.Seller;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISellerService, SellerService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        return services;
    }
}
