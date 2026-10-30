namespace User.Api.Extensions;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using User.Api.Common.Converters;
using User.Api.Middleware;

public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddServiceDefaults(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services.AddServiceObservability(configuration, serviceName);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new PascalCaseJsonStringEnumConverter(allowIntegerValues: false));
        });
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new PascalCaseJsonStringEnumConverter(allowIntegerValues: false));
        });

        return services;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseGlobalExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapGet("/", () => Results.Redirect("/swagger"));
        }

        return app;
    }
}
