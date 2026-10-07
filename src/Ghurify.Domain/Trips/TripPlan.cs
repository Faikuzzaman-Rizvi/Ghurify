using Ghurify.Domain.Identity;

namespace Ghurify.Domain.Trips;

/// <summary>One line of a trip's per-person cost breakdown.</summary>
public sealed record TripCostLineItem(CostCategory Category, string? Description, decimal Amount);

/// <summary>One day of a trip's plan.</summary>
public sealed record TripPlanDay(int DayNo, string Title, string Details, Difficulty Difficulty);

/// <summary>Why a trip cannot be saved or published as it stands.</summary>
public sealed record TripRuleViolation(string Code, string Message);

/// <summary>
/// A trip as its host has written it, and the rules it must meet. Saving a draft needs the plan
/// to be coherent; publishing needs it to be complete, the host to be verified, and the
/// destination to be open.
/// </summary>
public sealed class TripPlan(
    DateOnly startDate,
    DateOnly endDate,
    decimal pricePerPerson,
    GroupType groupType,
    IReadOnlyList<TripCostLineItem> costItems,
    IReadOnlyList<TripPlanDay> days)
{
    /// <summary>Longest trip a host may post. Longer journeys are split into several trips.</summary>
    public const int MaxDays = 30;

    public DateOnly StartDate { get; } = startDate;

    public DateOnly EndDate { get; } = endDate;

    public decimal PricePerPerson { get; } = pricePerPerson;

    public GroupType GroupType { get; } = groupType;

    public IReadOnlyList<TripCostLineItem> CostItems { get; } = costItems;

    public IReadOnlyList<TripPlanDay> Days { get; } = days;

    /// <summary>Days the trip lasts, counting the first and the last.</summary>
    public int Length => EndDate.DayNumber - StartDate.DayNumber + 1;

    /// <summary>
    /// Rules every save must meet, draft or not. Null when the plan is coherent.
    /// <paramref name="today"/> is today's date in Dhaka.
    /// </summary>
    public TripRuleViolation? CheckForSave(DateOnly today, Gender? hostGender)
    {
        if (EndDate < StartDate)
        {
            return new("end_before_start", "The trip cannot end before it starts.");
        }

        if (StartDate <= today)
        {
            return new("start_not_in_future", "A trip must start tomorrow or later.");
        }

        if (Length > MaxDays)
        {
            return new("trip_too_long", $"A trip can last at most {MaxDays} days.");
        }

        // The pricing promise: the price is exactly the lines, nothing hidden.
        if (CostItems.Sum(item => item.Amount) != PricePerPerson)
        {
            return new("price_mismatch", "The price per person must equal the sum of the cost breakdown.");
        }

        if (Days.Any(day => day.DayNo < 1 || day.DayNo > Length)
            || Days.Select(day => day.DayNo).Distinct().Count() != Days.Count)
        {
            return new("itinerary_out_of_range", "Each itinerary day must be a different day within the trip's dates.");
        }

        if (GroupType == GroupType.WomenOnly && hostGender != Gender.Female)
        {
            return new("women_only_host", "Only women hosts can run women-only trips.");
        }

        return null;
    }

    /// <summary>Everything <see cref="CheckForSave"/> checks, plus what going live needs.</summary>
    public TripRuleViolation? CheckForPublish(DateOnly today, DestinationStatus destinationStatus, UserAccess host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (CheckForSave(today, host.Gender) is { } violation)
        {
            return violation;
        }

        if (!host.IsVerifiedHost)
        {
            return new("host_not_verified", "Complete the national ID and selfie check before publishing.");
        }

        if (GroupType == GroupType.WomenOnly && !host.IsVerifiedWomanHost)
        {
            return new("women_only_host", "Only verified women hosts can publish women-only trips.");
        }

        if (destinationStatus == DestinationStatus.Closed)
        {
            return new("destination_closed", "This destination is closed by the safety desk. Trips there cannot be published.");
        }

        if (CostItems.Count == 0)
        {
            return new("costs_missing", "Add the cost breakdown before publishing.");
        }

        if (Days.Count != Length)
        {
            return new("itinerary_incomplete", "Plan every day of the trip before publishing.");
        }

        return null;
    }
}
