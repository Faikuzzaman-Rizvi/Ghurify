using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;

namespace Ghurify.UnitTests.Trips;

/// <summary>What a trip search may ask for.</summary>
public sealed class SearchTripsQueryValidatorTests
{
    private readonly SearchTripsQueryValidator _validator = new();

    [Fact]
    public void Validate_AnEmptySearch_IsAllowed()
    {
        Assert.True(_validator.Validate(new SearchTripsQuery()).IsValid);
    }

    [Fact]
    public void Validate_EveryFilterTogether_IsAllowed()
    {
        var query = new SearchTripsQuery(
            Destination: "sajek",
            From: new DateOnly(2026, 11, 1),
            To: new DateOnly(2026, 11, 30),
            MaxPrice: 8000,
            GroupType: GroupType.WomenOnly,
            MinSeats: 2,
            Sort: TripSort.PriceLowToHigh,
            Page: 2,
            PageSize: 24);

        Assert.True(_validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData("Sajek Valley")]
    [InlineData("sajek'; DROP TABLE")]
    public void Validate_ADestinationThatIsNotASlug_IsRefused(string destination)
    {
        Assert.False(_validator.Validate(new SearchTripsQuery(Destination: destination)).IsValid);
    }

    [Fact]
    public void Validate_ADateRangeThatEndsBeforeItStarts_IsRefused()
    {
        var query = new SearchTripsQuery(From: new DateOnly(2026, 11, 10), To: new DateOnly(2026, 11, 1));

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SearchTripsQuery.MaxPageSize + 1)]
    public void Validate_APageSizeOutOfRange_IsRefused(int pageSize)
    {
        Assert.False(_validator.Validate(new SearchTripsQuery(PageSize: pageSize)).IsValid);
    }

    [Fact]
    public void Validate_ANegativePrice_IsRefused()
    {
        Assert.False(_validator.Validate(new SearchTripsQuery(MaxPrice: -1)).IsValid);
    }

    [Fact]
    public void Validate_AGroupTypeThatDoesNotExist_IsRefused()
    {
        Assert.False(_validator.Validate(new SearchTripsQuery(GroupType: (GroupType)9)).IsValid);
    }
}
