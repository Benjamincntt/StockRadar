using StockRadar.Application.Common;

namespace StockRadar.Tests.Opportunities;

/// <summary>
/// Regression: GET Top must not hydrate an older date's list after today's run saved 0
/// (prod 2026-08-21 still returned VFS/SAB/SBT/KLB from 13/08 under zero_matches).
/// </summary>
public sealed class PreviousDayOpportunityFallbackTests
{
    [Fact]
    public void Today_saved_zero_does_not_attach_previous_day()
    {
        Assert.False(OpportunityAnalysisStatuses.AllowPreviousDayList(0, todayOpportunitiesSaved: 0));
    }

    [Fact]
    public void Today_not_analyzed_still_allows_previous_day()
    {
        Assert.True(OpportunityAnalysisStatuses.AllowPreviousDayList(0, todayOpportunitiesSaved: null));
    }

    [Fact]
    public void Today_has_items_does_not_need_previous_day()
    {
        Assert.False(OpportunityAnalysisStatuses.AllowPreviousDayList(4, todayOpportunitiesSaved: 4));
    }

    [Fact]
    public void Inconsistent_saved_positive_but_empty_cache_still_allows_previous_day()
    {
        Assert.True(OpportunityAnalysisStatuses.AllowPreviousDayList(0, todayOpportunitiesSaved: 3));
    }
}
