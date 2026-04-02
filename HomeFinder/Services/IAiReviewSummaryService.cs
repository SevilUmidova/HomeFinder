using HomeFinder.Models;

namespace HomeFinder.Services;

public interface IAiReviewSummaryService
{
    Task<ReviewSummaryFetchResult> GetSummaryAsync(
        int apartmentId,
        IReadOnlyCollection<ReviewApartment> reviews,
        CancellationToken cancellationToken = default);
}

