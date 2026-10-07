using Ghurify.Application.Abstractions;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Trips;

/// <summary>What creating and updating a trip share: resolving the destination and the rule check.</summary>
internal static class TripWriting
{
    public static TripPlan ToPlan(SaveTripCommand command) => new(
        command.StartDate,
        command.EndDate,
        command.PricePerPerson,
        command.GroupType,
        [.. command.CostItems.Select(item => new TripCostLineItem(item.Category, item.Description, item.Amount))],
        [.. command.Itinerary.Select(day => new TripPlanDay(day.DayNo, day.Title, day.Details, day.Difficulty))]);

    public static TripWrite ToWrite(SaveTripCommand command, long destinationId) => new(
        destinationId,
        command.Title.Trim(),
        command.Summary.Trim(),
        command.StartDate,
        command.EndDate,
        command.MeetingPoint.Trim(),
        command.Seats,
        command.PricePerPerson,
        command.GroupType,
        [.. command.CostItems.Select(item => item with { Description = Clean(item.Description) })],
        [.. command.Itinerary
            .OrderBy(day => day.DayNo)
            .Select(day => day with { Title = day.Title.Trim(), Details = day.Details.Trim() })]);

    public static AppError ToError(TripRuleViolation violation) => AppError.Rule(violation.Code, violation.Message);

    public static AppError UnknownDestination() =>
        AppError.Validation("destination_unknown", "Choose one of the listed destinations.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
