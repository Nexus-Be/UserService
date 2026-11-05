namespace User.Api.Filters;

using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using User.Api.Extensions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public RequireRoleAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles ?? Array.Empty<string>();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var userId = httpContext.GetUserId();

        if (userId == null || userId == Guid.Empty)
        {
            var traceId = httpContext.GetTraceId();
            context.Result = new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                errorCode = "UNAUTHORIZED",
                detail = "Authentication is required to access this resource.",
                traceId
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ContentTypes = { "application/problem+json" }
            };
            return;
        }

        if (_allowedRoles.Length > 0)
        {
            var userRoles = httpContext.GetUserRoles();
            var isAuthorized = _allowedRoles.Any(role =>
            {
                var target = role.Trim().ToLowerInvariant();
                if (userRoles.Contains(target, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Seller automatically inherits all Buyer capabilities (Hierarchical RBAC)
                if (target == "buyer" && userRoles.Contains("seller", StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (target == "staff" && (userRoles.Contains("support_staff", StringComparer.OrdinalIgnoreCase) || userRoles.Contains("support staff", StringComparer.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if (target == "admin" && userRoles.Contains("administrator", StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            });

            if (!isAuthorized)
            {
                var traceId = httpContext.GetTraceId();
                context.Result = new ObjectResult(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    title = "Forbidden",
                    status = StatusCodes.Status403Forbidden,
                    errorCode = "FORBIDDEN",
                    detail = $"Access denied. Required roles: [{string.Join(", ", _allowedRoles)}].",
                    traceId
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    ContentTypes = { "application/problem+json" }
                };
                return;
            }
        }
    }
}
