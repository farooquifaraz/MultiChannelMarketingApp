using FluentAssertions;
using MarketingApp.Application.Services;

namespace MarketingApp.Tests.Services;

/// <summary>CSV line parsing for contact import — quoted fields with commas must not shift columns.</summary>
public class ContactCsvParseTests
{
    [Fact]
    public void Splits_plain_row()
    {
        ContactService.ParseCsvLine("Ajmer School,a@b.com,+91999,+91999")
            .Should().Equal("Ajmer School", "a@b.com", "+91999", "+91999");
    }

    [Fact]
    public void Keeps_comma_inside_quotes()
    {
        var f = ContactService.ParseCsvLine("\"Ajeet Public School, Tijara\",ajit@x.com,+918502906690,+918502906690");
        f.Should().HaveCount(4);
        f[0].Should().Be("Ajeet Public School, Tijara");   // comma preserved, no column shift
        f[1].Should().Be("ajit@x.com");
        f[2].Should().Be("+918502906690");
    }

    [Fact]
    public void Handles_escaped_double_quote()
    {
        ContactService.ParseCsvLine("\"He said \"\"hi\"\"\",x@y.com")[0].Should().Be("He said \"hi\"");
    }

    [Fact]
    public void Trailing_empty_fields_preserved()
    {
        ContactService.ParseCsvLine("Name,email@x.com,,")
            .Should().Equal("Name", "email@x.com", "", "");
    }
}
