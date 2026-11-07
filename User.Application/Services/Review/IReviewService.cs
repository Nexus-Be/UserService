namespace User.Application.Services.Review;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;
using User.Domain.Enums;

public interface IReviewService
{
    Task<CreateReviewResponse> CreateReviewAsync(
        Guid reviewerId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedData<UserReviewItemDto>> GetUserReviewsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        int? rating = null,
        ReviewerRole? reviewerRole = null,
        CancellationToken cancellationToken = default);

    Task<HideReviewResponse> HideReviewAsync(
        Guid reviewId,
        HideReviewRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
