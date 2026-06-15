using FluentValidation;
using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Validators;

public class CreateCampaignValidator : AbstractValidator<CreateCampaignDto>
{
    public CreateCampaignValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Campaign name is required")
            .MaximumLength(150).WithMessage("Name must be 150 characters or less")
            // A campaign name is just a label and is often the email subject (emoji, %, !, ? etc. are
            // common). Only block control characters and angle brackets (the latter to avoid any HTML
            // injection concerns); everything else — including Unicode/emoji — is allowed.
            .Must(n => n != null && !n.Any(char.IsControl) && !n.Contains('<') && !n.Contains('>'))
            .WithMessage("Name cannot contain control characters or angle brackets");

        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("Template is required");

        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(c => new[] { "email", "whatsapp", "sms" }.Contains(c.ToLowerInvariant()))
            .WithMessage("Channel must be: email, whatsapp, or sms");

        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Contact group is required");

        RuleFor(x => x.ScheduledAt)
            .GreaterThan(DateTime.UtcNow.AddMinutes(5))
            .When(x => x.ScheduledAt.HasValue)
            .WithMessage("Scheduled time must be at least 5 minutes in the future");
    }
}
