namespace HomeFinder.Models;

public class ReviewSummaryFetchResult
{
    public string Status { get; set; } = "processing";

    public string Message { get; set; } = string.Empty;

    public DateTime? GeneratedAtUtc { get; set; }

    public ReviewSummaryViewModel? Summary { get; set; }
}

