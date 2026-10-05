using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.UnitTests.Trips;

/// <summary>Who is shown women-only trips.</summary>
public sealed class TripVisibilityTests
{
    [Theory]
    [InlineData(Gender.Female)]
    [InlineData(Gender.Unspecified)]
    public void WomenOnlyTrips_AreShownToViewersWhoMayJoinOrHaveNotSaid(Gender gender)
    {
        Assert.True(TripVisibility.IncludesWomenOnlyTrips(gender));
    }

    [Fact]
    public void WomenOnlyTrips_AreShownToSignedOutVisitors()
    {
        // Eligibility is enforced at the join step; hiding the feature from every visitor
        // would hide one of the product's safety promises.
        Assert.True(TripVisibility.IncludesWomenOnlyTrips(viewerGender: null));
    }

    [Theory]
    [InlineData(Gender.Male)]
    [InlineData(Gender.Other)]
    public void WomenOnlyTrips_AreHiddenFromViewersWhoCannotJoinThem(Gender gender)
    {
        Assert.False(TripVisibility.IncludesWomenOnlyTrips(gender));
    }
}
