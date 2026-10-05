using FluentValidation;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>
/// Shape checks at the API boundary. Whether a code is <em>correct</em> is decided against the
/// database in the handler; this only rejects input that cannot possibly be valid.
/// </summary>
public sealed class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .MaximumLength(EmailAddress.MaxLength)
            .Must(BeAnEmailAddress)
            .WithMessage("Enter a valid email address, for example rizvi@example.com.");
    }

    private static bool BeAnEmailAddress(string email) => EmailAddress.TryParse(email, out _);
}

public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .MaximumLength(EmailAddress.MaxLength)
            .Must(email => EmailAddress.TryParse(email, out _))
            .WithMessage("Enter a valid email address, for example rizvi@example.com.");

        RuleFor(command => command.Code)
            .NotEmpty().WithMessage("Enter the code we sent you.")
            .Matches("^[0-9]{4,8}$").WithMessage("The code is digits only.");
    }
}
