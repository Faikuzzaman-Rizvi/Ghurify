using System.Text.RegularExpressions;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// Adds a destination or edits its details, in both languages (audited). The slug is the
/// destination's address on the site, so it is fixed once chosen. Its safety status is changed
/// through the destination alert flow, not here.
/// </summary>
public sealed partial class SaveDestinationHandler(IAdminRepository admin, AccessService access, IAuditLog audit)
{
    public async Task<Result<DestinationSaved>> HandleAsync(long actorId, DestinationEdit edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);

        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
        {
            return AppError.Forbidden();
        }

        if (Validate(edit) is { } problem)
        {
            return problem;
        }

        var clean = edit with
        {
            Name = edit.Name.Trim(),
            NameBn = edit.NameBn.Trim(),
            Division = edit.Division.Trim(),
            DivisionBn = edit.DivisionBn.Trim(),
            Summary = edit.Summary.Trim(),
            SummaryBn = edit.SummaryBn.Trim(),
        };

        var added = await admin.SetDestinationAsync(clean, actorId, cancellationToken);
        await audit.WriteAsync(actorId, added ? "destination.added" : "destination.edited", "Destination", 0, clean.Slug, cancellationToken);
        return new DestinationSaved(clean.Slug, added);
    }

    private static AppError? Validate(DestinationEdit edit)
    {
        if (edit.Slug is not { Length: >= 3 and <= 60 } || !SlugPattern().IsMatch(edit.Slug))
        {
            return AppError.Validation("destination_slug", "Use 3 to 60 lower-case letters, digits and dashes, e.g. sajek-valley.");
        }

        if (!Enum.IsDefined(edit.Kind))
        {
            return AppError.Validation("destination_kind", "Choose what kind of place it is.");
        }

        if (Missing(edit.Name, 100) || Missing(edit.NameBn, 100) || Missing(edit.Division, 50) || Missing(edit.DivisionBn, 50)
            || Missing(edit.Summary, 400) || Missing(edit.SummaryBn, 400))
        {
            return AppError.Validation("destination_text", "Fill in the name, division and summary in both languages.");
        }

        if ((edit.Latitude is null) != (edit.Longitude is null)
            || edit.Latitude is < 20m or > 27m || edit.Longitude is < 88m or > 93m)
        {
            return AppError.Validation("destination_position", "Give both latitude and longitude, inside Bangladesh, or neither.");
        }

        return null;
    }

    private static bool Missing(string? value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

public sealed record DestinationSaved(string Slug, bool Added);
