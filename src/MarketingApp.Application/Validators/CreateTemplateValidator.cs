using FluentValidation;
using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Validators;

public class CreateTemplateValidator : AbstractValidator<CreateTemplateDto>
{
    public CreateTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(c => new[] { "email", "whatsapp", "sms", "instagram", "facebook" }.Contains(c.ToLowerInvariant()))
            .WithMessage("Channel must be: email, whatsapp, sms, instagram, or facebook");
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required for email templates")
            .When(x => x.Channel.Equals("email", StringComparison.OrdinalIgnoreCase));
    }
}
