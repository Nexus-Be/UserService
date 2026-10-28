namespace User.Application.Services.Admin;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IAdminUserService
{
    Task<AdminUserCreatedResponseDto> CreateUserAsync(
        CreateAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    Task<AdminUserListPagedResultDto> GetUsersAsync(
        int page = 1,
        int limit = 20,
        string? keyword = null,
        string? role = null,
        string? status = null,
        CancellationToken cancellationToken = default
    );

    Task<AdminUserDetailDto> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );
}
