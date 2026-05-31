using System.Net;
using FluentAssertions;

namespace MarketingApp.Tests.Regression;

/// <summary>
/// Contacts CRUD + ILIKE search + bulk-ish operations. Covers Zero-Regression
/// items #14 (CRUD + search) and confirms the ILIKE fix from the QA cycle stays in.
/// </summary>
[Collection("Regression")]
public class ContactsRegressionTests(RegressionTestFactory factory) : IClassFixture<RegressionTestFactory>
{
    private async Task<string> EnsureAdminTokenAsync()
    {
        const string email = "contacts.admin@regression.test";
        const string pwd = "RegPass!12345";

        await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Contacts Admin",
            email,
            password = pwd
        });
        var login = await factory.PostJsonAsync<AuthResp>("/api/v1/auth/login", new { email, password = pwd });
        return login!.Data!.AccessToken;
    }

    [Fact(DisplayName = "Create + Get cycle")]
    public async Task CrudCycle()
    {
        var jwt = await EnsureAdminTokenAsync();

        var created = await factory.PostJsonAsync<ContactDto>("/api/v1/contacts", new
        {
            fullName = "Crud Tester",
            email = "crud.contact@regression.test",
            phone = "+971500999111"
        }, jwt);
        created!.Success.Should().BeTrue(created.Message);
        var id = created.Data!.Id;

        var getResult = await factory.GetRawAsync($"/api/v1/contacts/{id}", jwt);
        getResult.status.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Duplicate email rejected with 4xx (not 500)")]
    public async Task DuplicateContactEmailRejected()
    {
        var jwt = await EnsureAdminTokenAsync();
        const string email = "duplicate.contact@regression.test";

        await factory.PostJsonAsync<ContactDto>("/api/v1/contacts", new
        {
            fullName = "Original",
            email,
            phone = "+971500999112"
        }, jwt);

        var (status, _) = await factory.PostRawAsync("/api/v1/contacts", new
        {
            fullName = "Imposter",
            email,
            phone = "+971500999113"
        }, jwt);

        ((int)status).Should().BeOneOf(400, 409);
    }

    [Fact(DisplayName = "Invalid email is rejected by validation (400)")]
    public async Task InvalidEmailRejected()
    {
        var jwt = await EnsureAdminTokenAsync();
        var (status, _) = await factory.PostRawAsync("/api/v1/contacts", new
        {
            fullName = "Bad Email",
            email = "not-an-email",
            phone = "+971500999114"
        }, jwt);
        status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "ILIKE — case-insensitive search finds rows by uppercase fragment")]
    public async Task IlikeSearchWorks()
    {
        var jwt = await EnsureAdminTokenAsync();

        // create a contact whose name has a known lowercase substring
        await factory.PostJsonAsync<ContactDto>("/api/v1/contacts", new
        {
            fullName = "ilike searchcoverage subject",
            email = "ilike.search@regression.test",
            phone = "+971500999115"
        }, jwt);

        var (status, body) = await factory.GetRawAsync(
            "/api/v1/contacts?search=SEARCHCOVERAGE", jwt);
        status.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("ilike.search@regression.test",
            "ILIKE must be case-insensitive — uppercase fragment must still match");
    }

    [Fact(DisplayName = "Search by phone substring works")]
    public async Task SearchByPhone()
    {
        var jwt = await EnsureAdminTokenAsync();

        await factory.PostJsonAsync<ContactDto>("/api/v1/contacts", new
        {
            fullName = "Phone Searchable",
            email = "phone.search@regression.test",
            phone = "+971500777999"
        }, jwt);

        var (status, body) = await factory.GetRawAsync("/api/v1/contacts?search=777999", jwt);
        status.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("phone.search@regression.test");
    }
}

// Inner data shape — PostJsonAsync<T> wraps in ApiEnvelope<T>.
public record ContactDto(Guid Id, string FullName, string? Email, string? Phone);
