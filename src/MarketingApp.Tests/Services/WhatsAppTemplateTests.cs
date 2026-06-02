using System.Text.Json;
using FluentAssertions;
using MarketingApp.Infrastructure.Services;

namespace MarketingApp.Tests.Services;

/// <summary>
/// L2 — WhatsApp approved-template message payload shaping + Meta Graph response parsing.
/// Both are pure functions, so we can fully verify them without Meta credentials.
/// </summary>
public class WhatsAppTemplateTests
{
    private static JsonElement Json(Dictionary<string, object?> p) =>
        JsonDocument.Parse(JsonSerializer.Serialize(p)).RootElement;

    // ---------- BuildTemplatePayload ----------

    [Fact(DisplayName = "Template payload: type=template, name + language code")]
    public void TemplatePayload_Basic()
    {
        var p = Json(WhatsAppCloudService.BuildTemplatePayload("971500000000", "hello_world", "en_US", new List<string>()));
        p.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        p.GetProperty("to").GetString().Should().Be("971500000000");
        p.GetProperty("type").GetString().Should().Be("template");
        var t = p.GetProperty("template");
        t.GetProperty("name").GetString().Should().Be("hello_world");
        t.GetProperty("language").GetProperty("code").GetString().Should().Be("en_US");
        // No variables → no components array.
        t.TryGetProperty("components", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Template payload: body parameters fill {{1}},{{2}} in order")]
    public void TemplatePayload_WithParams()
    {
        var p = Json(WhatsAppCloudService.BuildTemplatePayload("971500000000", "property_listing", "en", new List<string> { "Ahsan", "Villa 12" }));
        var comps = p.GetProperty("template").GetProperty("components");
        comps.GetArrayLength().Should().Be(1);
        var body = comps[0];
        body.GetProperty("type").GetString().Should().Be("body");
        var ps = body.GetProperty("parameters");
        ps.GetArrayLength().Should().Be(2);
        ps[0].GetProperty("type").GetString().Should().Be("text");
        ps[0].GetProperty("text").GetString().Should().Be("Ahsan");
        ps[1].GetProperty("text").GetString().Should().Be("Villa 12");
    }

    [Fact(DisplayName = "Template payload: blank language defaults to en_US")]
    public void TemplatePayload_DefaultLanguage()
    {
        var p = Json(WhatsAppCloudService.BuildTemplatePayload("971500000000", "x", "", new List<string>()));
        p.GetProperty("template").GetProperty("language").GetProperty("code").GetString().Should().Be("en_US");
    }

    // ---------- ParseMetaTemplates ----------

    private static readonly Guid Gid = Guid.Parse("99999999-8888-7777-6666-555555555555");

    [Fact(DisplayName = "Parse: extracts body text, variable count, header type, status, category")]
    public void Parse_FullTemplate()
    {
        var json = """
        {
          "data": [
            {
              "name": "property_listing",
              "language": "en_US",
              "status": "APPROVED",
              "category": "MARKETING",
              "id": "123456",
              "components": [
                { "type": "HEADER", "format": "IMAGE" },
                { "type": "BODY", "text": "Hi {{1}}, new listing {{2}} priced at {{3}}." },
                { "type": "FOOTER", "text": "SAM Digital" }
              ]
            }
          ]
        }
        """;
        var list = WhatsAppTemplateService.ParseMetaTemplates(json, Gid);
        list.Should().HaveCount(1);
        var t = list[0];
        t.SmtpGroupId.Should().Be(Gid);
        t.Name.Should().Be("property_listing");
        t.Language.Should().Be("en_US");
        t.Status.Should().Be("APPROVED");
        t.Category.Should().Be("MARKETING");
        t.HeaderType.Should().Be("IMAGE");
        t.VariableCount.Should().Be(3);
        t.BodyText.Should().Contain("{{1}}");
        t.MetaTemplateId.Should().Be("123456");
    }

    [Fact(DisplayName = "Parse: counts DISTINCT variables (repeated {{1}} = 1)")]
    public void Parse_DistinctVariables()
    {
        var json = """
        { "data": [ { "name": "t", "language": "en", "status": "APPROVED",
          "components": [ { "type": "BODY", "text": "Hi {{1}}, bye {{1}}" } ] } ] }
        """;
        var list = WhatsAppTemplateService.ParseMetaTemplates(json, Gid);
        list[0].VariableCount.Should().Be(1);
    }

    [Fact(DisplayName = "Parse: no variables → count 0, no header")]
    public void Parse_NoVariables()
    {
        var json = """
        { "data": [ { "name": "t", "language": "en", "status": "PENDING",
          "components": [ { "type": "BODY", "text": "Static message" } ] } ] }
        """;
        var list = WhatsAppTemplateService.ParseMetaTemplates(json, Gid);
        list[0].VariableCount.Should().Be(0);
        list[0].HeaderType.Should().BeNull();
        list[0].Status.Should().Be("PENDING");
    }

    [Fact(DisplayName = "Parse: empty / no data array → empty list (no throw)")]
    public void Parse_Empty()
    {
        WhatsAppTemplateService.ParseMetaTemplates("{}", Gid).Should().BeEmpty();
        WhatsAppTemplateService.ParseMetaTemplates("""{"data":[]}""", Gid).Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse: multiple templates")]
    public void Parse_Multiple()
    {
        var json = """
        { "data": [
          { "name": "a", "language": "en", "status": "APPROVED", "components": [ { "type":"BODY","text":"A {{1}}" } ] },
          { "name": "b", "language": "ar", "status": "REJECTED", "components": [ { "type":"BODY","text":"B" } ] }
        ] }
        """;
        var list = WhatsAppTemplateService.ParseMetaTemplates(json, Gid);
        list.Should().HaveCount(2);
        list.Should().Contain(t => t.Name == "a" && t.VariableCount == 1 && t.Status == "APPROVED");
        list.Should().Contain(t => t.Name == "b" && t.Language == "ar" && t.Status == "REJECTED");
    }
}
