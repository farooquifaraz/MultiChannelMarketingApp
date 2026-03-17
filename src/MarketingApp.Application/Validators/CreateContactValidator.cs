using FluentValidation;
using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Validators;

public class CreateContactValidator : AbstractValidator<CreateContactDto>
{
    public CreateContactValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email)).MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.WhatsAppNumber).MaximumLength(20);
        RuleFor(x => x).Must(x => !string.IsNullOrEmpty(x.Email) || !string.IsNullOrEmpty(x.Phone) || !string.IsNullOrEmpty(x.WhatsAppNumber))
            .WithMessage("At least one contact method (email, phone, or WhatsApp) is required.");
    }
}
