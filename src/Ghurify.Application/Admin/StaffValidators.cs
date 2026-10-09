using FluentValidation;

namespace Ghurify.Application.Admin;

/// <summary>
/// Shape checks for the role editor. What a role may <em>contain</em> is decided in
/// <see cref="SaveStaffRoleHandler"/>, which knows who is asking; this only checks that the
/// fields fit the columns and that the key is a usable slug.
/// </summary>
public sealed class SaveStaffRoleCommandValidator : AbstractValidator<SaveStaffRoleCommand>
{
    public SaveStaffRoleCommandValidator()
    {
        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(40)
            // Lowercase slug: it appears in audit rows and is matched by the seed script, so it
            // must read the same everywhere and never need escaping.
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("A role key is lowercase letters, numbers and single hyphens, like \"payments-desk\".");

        RuleFor(command => command.Name).NotEmpty().MaximumLength(60);
        RuleFor(command => command.NameBn).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Description).MaximumLength(300);
        RuleFor(command => command.DescriptionBn).MaximumLength(300);

        RuleFor(command => command.Permissions)
            .NotNull()
            // The role editor shows about thirty; anything near this is a malformed request.
            .Must(permissions => permissions.Count <= 200)
            .WithMessage("That is more permissions than exist.");

        RuleForEach(command => command.Permissions)
            .NotEmpty()
            .MaximumLength(40);
    }
}
