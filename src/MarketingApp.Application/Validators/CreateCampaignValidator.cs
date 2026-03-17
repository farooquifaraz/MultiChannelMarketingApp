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
            .Matches(@"^[a-zA-Z0-9\s\-_/,.:()#]+$").WithMessage("Name contains invalid characters");

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
