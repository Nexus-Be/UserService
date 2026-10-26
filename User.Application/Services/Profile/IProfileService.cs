namespace User.Application.Services.Profile;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IProfileService
{
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}
