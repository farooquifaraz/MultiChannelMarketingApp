using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace MarketingApp.Tests.Regression;

/// <summary>
/// BUG-003 regression: malformed paging params (negative pageNumber, huge pageSize)
/// used to crash with HTTP 500. The PagingHelper.Clamp fix clamps them defensively;
/// these tests guard against regression on every paged endpoint.
/// </summary>
[Collection("Regression")]
public class PaginationRegressionTests(RegressionTestFactory factory) : IClassFixture<RegressionTestFactory>
{
    private async Task<string> EnsureAdminTokenAsync()
    {
        // Register and login as admin (first user = admin per BUG-001 fix)
        const string email = "paging.admin@regression.test";
        const string pwd = "RegPass!12345";

        await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Paging Admin",
            email,
            password = pwd
        });
        var login = await factory.PostJsonAsync<AuthResp>("/api/v1/auth/login", new { email, password = pwd });
        return login!.Data!.AccessToken;
    }

    public static TheoryData<string> PagedEndpoints =>
        new()
        {
            "/api/v1/contacts",
            "/api/v1/campaigns",
            "/api/v1/admin/audit-logs",
            "/api/v1/inbox",
            "/api/v1/inbox/threads",
        };

    [Theory(DisplayName = "BUG-003 — negative pageNumber does NOT 500")]
    [MemberData(nameof(PagedEndpoints))]
    public async Task NegativePageNumberHandled(string endpoint)
    {
        var jwt = await EnsureAdminTokenAsync();
        var (status, _) = await factory.GetRawAsync($"{endpoint}?pageNumber=-1&pageSize=5", jwt);

        ((int)status).Should().BeLessThan(500,
            $"BUG-003 fix: negative pageNumber on {endpoint} should be clamped, not crash");
    }

    [Theory(DisplayName = "BUG-003 — negative pageSize does NOT 500")]
    [MemberData(nameof(PagedEndpoints))]
    public async Task NegativePageSizeHandled(string endpoint)
    {
        var jwt = await EnsureAdminTokenAsync();
        var (status, _) = await factory.GetRawAsync($"{endpoint}?pageNumber=1&pageSize=-5", jwt);

        ((int)status).Should().BeLessThan(500);
    }

    [Theory(DisplayName = "BUG-003 — huge pageSize clamped at MaxPageSize (200)")]
    [MemberData(nameof(PagedEndpoints))]
    public async Task HugePageSizeClamped(string endpoint)
    {
        var jwt = await EnsureAdminTokenAsync();
        var (status, body) = await factory.GetRawAsync($"{endpoint}?pageNumber=1&pageSize=99999", jwt);

        ((int)status).Should().Be(200);
        var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            data.GetArrayLength().Should().BeLessThanOrEqualTo(200,
                "MaxPageSize=200 in PagingHelper");
        }
    }
}
