using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.UnitTests.Trips;

/// <summary>The rules a trip must meet to be saved, and the stricter ones to go live.</summary>
public sealed class TripPlanTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public void CheckForSave_WhenThePriceIsNotTheSumOfTheLines_RefusesWithPriceMismatch()
    {
        var plan = Plan(price: 5000, costs: [2800, 1800]);

        Assert.Equal("price_mismatch", plan.CheckForSave(Today, Gender.Female)?.Code);
    }

    [Fact]
    public void CheckForSave_WhenThePriceIsExactlyTheSumOfTheLines_Passes()
    {
        Assert.Null(Plan(price: 4600, costs: [2800, 1800]).CheckForSave(Today, Gender.Female));
    }

    [Fact]
    public void CheckForSave_ATripStartingToday_IsRefused()
    {
        var plan = Plan(start: Today, end: Today.AddDays(1));

        Assert.Equal("start_not_in_future", plan.CheckForSave(Today, Gender.Female)?.Code);
    }

    [Fact]
    public void CheckForSave_ATripEndingBeforeItStarts_IsRefused()
    {
        var plan = Plan(start: Today.AddDays(10), end: Today.AddDays(9));

        Assert.Equal("end_before_start", plan.CheckForSave(Today, Gender.Female)?.Code);
    }

    [Fact]
    public void CheckForSave_AnItineraryDayOutsideTheTripDates_IsRefused()
    {
        var plan = Plan(days: [1, 2, 5]);

        Assert.Equal("itinerary_out_of_range", plan.CheckForSave(Today, Gender.Female)?.Code);
    }

    [Fact]
    public void CheckForSave_AWomenOnlyTripByAMaleHost_IsRefused()
    {
        var plan = Plan(group: GroupType.WomenOnly);

        Assert.Equal("women_only_host", plan.CheckForSave(Today, Gender.Male)?.Code);
    }

    [Fact]
    public void CheckForPublish_ByAnUnverifiedHost_IsRefused()
    {
        var host = Host(verified: VerificationLevel.Nid);

        Assert.Equal("host_not_verified", Plan().CheckForPublish(Today, DestinationStatus.Open, host)?.Code);
    }

    [Fact]
    public void CheckForPublish_ToAClosedDestination_IsRefused()
    {
        Assert.Equal("destination_closed", Plan().CheckForPublish(Today, DestinationStatus.Closed, Host())?.Code);
    }

    [Fact]
    public void CheckForPublish_ToADestinationUnderCaution_IsAllowed()
    {
        Assert.Null(Plan().CheckForPublish(Today, DestinationStatus.Caution, Host()));
    }

    [Fact]
    public void CheckForPublish_WithADayLeftUnplanned_IsRefused()
    {
        var plan = Plan(days: [1, 2]);

        Assert.Equal("itinerary_incomplete", plan.CheckForPublish(Today, DestinationStatus.Open, Host())?.Code);
    }

    [Fact]
    public void CheckForPublish_AWomenOnlyTripByAVerifiedWomanHost_IsAllowed()
    {
        Assert.Null(Plan(group: GroupType.WomenOnly).CheckForPublish(Today, DestinationStatus.Open, Host(Gender.Female)));
    }

    private static UserAccess Host(Gender gender = Gender.Female, VerificationLevel verified = VerificationLevel.NidSelfie) =>
        new(7, UserStatus.Active, gender, new HashSet<Role> { Role.Host }, verified);

    /// <summary>A three-day trip starting in ten days, priced at the sum of its lines, fully planned.</summary>
    private static TripPlan Plan(
        DateOnly? start = null,
        DateOnly? end = null,
        decimal? price = null,
        decimal[]? costs = null,
        int[]? days = null,
        GroupType group = GroupType.Open)
    {
        costs ??= [2800, 1800, 1500];
        days ??= [1, 2, 3];

        return new TripPlan(
            start ?? Today.AddDays(10),
            end ?? Today.AddDays(12),
            price ?? costs.Sum(),
            group,
            [.. costs.Select(amount => new TripCostLineItem(CostCategory.Transport, null, amount))],
            [.. days.Select(day => new TripPlanDay(day, $"Day {day}", "Plan.", Difficulty.Easy))]);
    }
}
