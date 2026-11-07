namespace User.Application.Services.AdminConfig;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;

public interface IAdminConfigService
{
    Task<PagedData<SystemConfigItemDto>> GetConfigsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<UpdateSystemConfigResponse> UpdateConfigAsync(
        UpdateSystemConfigRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
