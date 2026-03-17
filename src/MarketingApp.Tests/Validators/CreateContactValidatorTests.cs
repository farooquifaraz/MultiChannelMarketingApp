using FluentAssertions;
using FluentValidation.TestHelper;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Validators;

namespace MarketingApp.Tests.Validators;

public class CreateContactValidatorTests
{
    private readonly CreateContactValidator _validator = new();

    private static CreateContactDto ValidDto() => new()
    {
        FullName = "John Doe",
        Email = "john@example.com",
        Phone = "+1234567890",
        WhatsAppNumber = null,
        GroupId = Guid.NewGuid()
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
    public void FullName_WhenEmpty_ShouldFail(string? name)
    {
        var dto = ValidDto();
        dto.FullName = name!;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void FullName_WhenExceedsMaxLength_ShouldFail()
    {
        var dto = ValidDto();
        dto.FullName = new string('A', 101);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void FullName_AtMaxLength_ShouldPass()
    {
        var dto = ValidDto();
        dto.FullName = new string('A', 100);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.FullName);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void Email_WhenInvalid_ShouldFail(string email)
    {
        var dto = ValidDto();
        dto.Email = email;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_WhenValid_ShouldPass()
    {
        var dto = ValidDto();
        dto.Email = "test@example.com";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_WhenNullOrEmpty_ShouldPassIfOtherContactMethodExists()
    {
        var dto = ValidDto();
        dto.Email = null;
        dto.Phone = "+1234567890";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_WhenExceedsMaxLength_ShouldFail()
    {
        var dto = ValidDto();
        dto.Email = new string('a', 140) + "@example.com";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Phone_WhenExceedsMaxLength_ShouldFail()
    {
        var dto = ValidDto();
        dto.Phone = new string('1', 21);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void WhatsAppNumber_WhenExceedsMaxLength_ShouldFail()
    {
        var dto = ValidDto();
        dto.WhatsAppNumber = new string('1', 21);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.WhatsAppNumber);
    }

    [Fact]
    public void NoContactMethod_ShouldFail()
    {
        var dto = ValidDto();
        dto.Email = null;
        dto.Phone = null;
        dto.WhatsAppNumber = null;
        var result = _validator.TestValidate(dto);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one contact method"));
    }

    [Fact]
    public void OnlyWhatsApp_ShouldPass()
    {
        var dto = ValidDto();
        dto.Email = null;
        dto.Phone = null;
        dto.WhatsAppNumber = "+1234567890";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void OnlyPhone_ShouldPass()
    {
        var dto = ValidDto();
        dto.Email = null;
        dto.Phone = "+1234567890";
        dto.WhatsAppNumber = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void OnlyEmail_ShouldPass()
    {
        var dto = ValidDto();
        dto.Email = "test@example.com";
        dto.Phone = null;
        dto.WhatsAppNumber = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
