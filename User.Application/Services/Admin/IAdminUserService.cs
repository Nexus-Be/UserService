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
}
