using FluentAssertions;
using FluentValidation.TestHelper;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Validators;

namespace MarketingApp.Tests.Validators;

public class CreateCampaignValidatorTests
{
    private readonly CreateCampaignValidator _validator = new();

    private static CreateCampaignDto ValidDto() => new()
    {
        Name = "Summer Sale Campaign",
        Channel = "email",
        TemplateId = Guid.NewGuid(),
        GroupId = Guid.NewGuid(),
        ScheduledAt = null
    };

    [Fact]
    public void ValidInput_ShouldPassValidation()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Name_WhenEmpty_ShouldFail(string? name)
    {
        var dto = ValidDto();
        dto.Name = name!;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_WhenExceedsMaxLength_ShouldFail()
    {
        var dto = ValidDto();
        dto.Name = new string('A', 151);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_AtMaxLength_ShouldPass()
    {
        var dto = ValidDto();
        dto.Name = new string('A', 150);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_WithAngleBrackets_ShouldFail()
    {
        var dto = ValidDto();
        dto.Name = "Campaign <script>";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_WithControlCharacters_ShouldFail()
    {
        var dto = ValidDto();
        dto.Name = "Campaign\tName";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_WithValidSpecialChars_ShouldPass()
    {
        // Campaign names double as email subjects — emoji, %, !, ?, @, # are common and allowed.
        var dto = ValidDto();
        dto.Name = "Summer Sale 50% off! 🎉 @everyone";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("Email")]
    [InlineData("sms")]
    [InlineData("SMS")]
    [InlineData("whatsapp")]
    [InlineData("WhatsApp")]
    public void Channel_WhenValid_ShouldPass(string channel)
    {
        var dto = ValidDto();
        dto.Channel = channel;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Channel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fax")]
    [InlineData("telegram")]
    public void Channel_WhenInvalid_ShouldFail(string channel)
    {
        var dto = ValidDto();
        dto.Channel = channel;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Channel);
    }

    [Fact]
    public void TemplateId_WhenEmpty_ShouldFail()
    {
        var dto = ValidDto();
        dto.TemplateId = Guid.Empty;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TemplateId);
    }

    [Fact]
    public void GroupId_WhenEmpty_ShouldFail()
    {
        var dto = ValidDto();
        dto.GroupId = Guid.Empty;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.GroupId);
    }

    [Fact]
    public void ScheduledAt_WhenNull_ShouldPass()
    {
        var dto = ValidDto();
        dto.ScheduledAt = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ScheduledAt);
    }

    [Fact]
    public void ScheduledAt_WhenFarInFuture_ShouldPass()
    {
        var dto = ValidDto();
        dto.ScheduledAt = DateTime.UtcNow.AddHours(1);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ScheduledAt);
    }

    [Fact]
    public void ScheduledAt_WhenInPast_ShouldFail()
    {
        var dto = ValidDto();
        dto.ScheduledAt = DateTime.UtcNow.AddMinutes(-10);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ScheduledAt);
    }
}
