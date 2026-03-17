using FluentValidation;
using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Validators;

public class RegisterValidator : AbstractValidator<RegisterDto>
{
    public RegisterValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().MinimumLength(2).MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().MaximumLength(150);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
    }
}
