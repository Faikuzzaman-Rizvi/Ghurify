using FluentValidation;

namespace Ghurify.Application.Trips;

/// <summary>
/// Shape checks on a trip from the wizard. Rules that need context (dates against today, the
/// price against the lines, who the host is) live in <see cref="Domain.Trips.TripPlan"/>.
/// </summary>
public sealed class SaveTripCommandValidator : AbstractValidator<SaveTripCommand>
{
    public const int MaxSeats = 50;
    public const int MaxCostItems = 20;

    public SaveTripCommandValidator()
    {
        RuleFor(command => command.DestinationSlug)
            .NotEmpty()
            .MaximumLength(60)
            .Matches("^[a-z0-9-]+$").WithMessage("Choose a destination.");

        RuleFor(command => command.Title)
            .NotEmpty()
            .Must(title => title is not null && title.Trim().Length is >= 5 and <= 150)
            .WithMessage("The title must be between 5 and 150 characters.");

        RuleFor(command => command.Summary)
            .NotEmpty()
            .Must(summary => summary is not null && summary.Trim().Length is >= 20 and <= 1000)
            .WithMessage("Describe the trip in 20 to 1000 characters.");

        RuleFor(command => command.MeetingPoint)
            .NotEmpty()
            .Must(point => point is not null && point.Trim().Length is >= 3 and <= 200)
            .WithMessage("Say where the group meets, in up to 200 characters.");

        RuleFor(command => command.Seats).InclusiveBetween(1, MaxSeats);

        RuleFor(command => command.PricePerPerson)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1_000_000)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(command => command.GroupType).IsInEnum();

        RuleFor(command => command.CostItems)
            .NotNull()
            .Must(items => items is null || items.Count <= MaxCostItems)
            .WithMessage($"Use at most {MaxCostItems} cost lines.");

        RuleForEach(command => command.CostItems).ChildRules(line =>
        {
            line.RuleFor(item => item.Category).IsInEnum();
            line.RuleFor(item => item.Description).MaximumLength(150);
            line.RuleFor(item => item.Amount)
                .GreaterThanOrEqualTo(0)
                .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        });

        RuleFor(command => command.Itinerary)
            .NotNull()
            .Must(days => days is null || days.Count <= Domain.Trips.TripPlan.MaxDays)
            .WithMessage($"A trip has at most {Domain.Trips.TripPlan.MaxDays} days.");

        RuleForEach(command => command.Itinerary).ChildRules(day =>
        {
            day.RuleFor(item => item.DayNo).InclusiveBetween(1, Domain.Trips.TripPlan.MaxDays);
            day.RuleFor(item => item.Title).NotEmpty().MaximumLength(150);
            day.RuleFor(item => item.Details).NotEmpty().MaximumLength(1000);
            day.RuleFor(item => item.Difficulty).IsInEnum();
        });
    }
}
