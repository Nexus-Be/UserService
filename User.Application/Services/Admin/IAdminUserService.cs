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

    Task<PagedData<AdminUserListItemDto>> GetUsersAsync(
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

    Task<AdminRoleListResponseDto> GetRolesAsync(
        CancellationToken cancellationToken = default
    );

    Task<AdminUserRolesResponseDto> AssignUserRolesAsync(
        Guid targetUserId,
        AssignAdminUserRolesDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    Task<AdminUserUpdatedResponseDto> UpdateUserAsync(
        Guid targetUserId,
        UpdateAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    Task DeleteUserAsync(
        Guid targetUserId,
        DeleteAdminUserDto? dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );
}
