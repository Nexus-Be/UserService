namespace User.Application;

using Microsoft.Extensions.DependencyInjection;
using User.Application.Services.Admin;
using User.Application.Services.AdminConfig;
using User.Application.Services.Auth;
using User.Application.Services.Internal;
using User.Application.Services.Profile;
using User.Application.Services.Report;
using User.Application.Services.Reputation;
using User.Application.Services.Review;
using User.Application.Services.Seller;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISellerService, SellerService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminPenaltyService, AdminPenaltyService>();
        services.AddScoped<IReputationService, ReputationService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAdminConfigService, AdminConfigService>();
        services.AddScoped<IInternalUserService, InternalUserService>();
        return services;
    }
}
