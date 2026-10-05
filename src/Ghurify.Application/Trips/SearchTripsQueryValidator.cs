using FluentValidation;

namespace Ghurify.Application.Trips;

/// <summary>Shape checks on a trip search, before it reaches the database.</summary>
public sealed class SearchTripsQueryValidator : AbstractValidator<SearchTripsQuery>
{
    public SearchTripsQueryValidator()
    {
        RuleFor(query => query.Destination)
            .MaximumLength(60)
            .Matches("^[a-z0-9-]+$").WithMessage("Destination must be a destination slug, for example sajek.")
            .When(query => !string.IsNullOrWhiteSpace(query.Destination));

        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From!.Value)
            .WithMessage("The end of the date range must not be before its start.")
            .When(query => query.From is not null && query.To is not null);

        RuleFor(query => query.MaxPrice)
            .GreaterThanOrEqualTo(0).When(query => query.MaxPrice is not null);

        RuleFor(query => query.GroupType)
            .IsInEnum().When(query => query.GroupType is not null);

        RuleFor(query => query.MinSeats)
            .InclusiveBetween(1, 50).When(query => query.MinSeats is not null);

        RuleFor(query => query.Sort).IsInEnum();

        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, SearchTripsQuery.MaxPageSize);
    }
}
