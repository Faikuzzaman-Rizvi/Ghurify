using FluentValidation;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>
/// Shape checks at the API boundary. Whether a password or code is <em>correct</em> (and whether
/// a new password is strong enough) is decided in the handler; these only reject input that
/// cannot possibly be valid.
/// </summary>
internal static class AccountRules
{
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Email address is required.")
            .MaximumLength(EmailAddress.MaxLength)
            .Must(email => EmailAddress.TryParse(email, out _))
            .WithMessage("Enter a valid email address, for example rizvi@example.com.");

    public static IRuleBuilderOptions<T, string> ValidCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Enter the code we sent you.")
            .Matches("^[0-9]{4,8}$").WithMessage("The code is digits only.");

    public static IRuleBuilderOptions<T, string> PresentPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Enter your password.")
            .MaximumLength(PasswordPolicy.MaxLength);
}

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).PresentPassword();
        RuleFor(command => command.DisplayName)
            .Must(name => name is not null && name.Trim().Length is >= 2 and <= 100)
            .WithMessage("Your name must be between 2 and 100 characters.");
    }
}

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Code).ValidCode();
    }
}

public sealed class EmailOnlyCommandValidator : AbstractValidator<EmailOnlyCommand>
{
    public EmailOnlyCommandValidator() => RuleFor(command => command.Email).ValidEmail();
}

public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).PresentPassword();
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Code).ValidCode();
        RuleFor(command => command.NewPassword).PresentPassword();
    }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).MaximumLength(PasswordPolicy.MaxLength);
        RuleFor(command => command.NewPassword).PresentPassword();
    }
}

/// <summary>Shape checks on a profile edit. Phone numbers must be Bangladeshi mobiles.</summary>
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .Must(name => name is not null && name.Trim().Length is >= 2 and <= 100)
            .WithMessage("Your name must be between 2 and 100 characters.");

        RuleFor(command => command.Gender).IsInEnum().When(command => command.Gender is not null);

        RuleFor(command => command.Phone)
            .Must(phone => PhoneNumber.TryParse(phone, out _))
            .WithMessage("Enter a Bangladeshi mobile number, for example 01712345678.")
            .When(command => !string.IsNullOrWhiteSpace(command.Phone));

        RuleFor(command => command.Bio).MaximumLength(500);
        RuleFor(command => command.HomeDistrict).MaximumLength(60);
        RuleFor(command => command.EmergencyContactName).MaximumLength(100);

        RuleFor(command => command.EmergencyContactPhone)
            .Must(phone => PhoneNumber.TryParse(phone, out _))
            .WithMessage("Enter a Bangladeshi mobile number for your emergency contact.")
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactPhone));

        // A contact is a name and a number; half of one helps nobody in an emergency.
        RuleFor(command => command.EmergencyContactPhone)
            .NotEmpty()
            .WithMessage("Add a phone number for your emergency contact.")
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactName));
    }
}
