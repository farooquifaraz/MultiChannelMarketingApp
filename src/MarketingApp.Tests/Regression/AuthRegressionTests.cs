using System.Net;
using FluentAssertions;

namespace MarketingApp.Tests.Regression;

/// <summary>
/// Auth + JWT regression coverage. Mirrors items #1 in the Zero-Regression Checklist
/// (login, JWT issuance, refresh, anonymous rejection, tampered token rejection).
/// Also covers the BUG-001 fix — first registered user becomes admin on a fresh DB.
/// </summary>
[Collection("Regression")]
public class AuthRegressionTests(RegressionTestFactory factory) : IClassFixture<RegressionTestFactory>
{
    [Fact(DisplayName = "BUG-001 — first user on a fresh DB becomes admin")]
    public async Task FirstUserBecomesAdmin()
    {
        // Other tests in the collection share the DB, so explicitly wipe users
        // first to recreate the "fresh DB" precondition this test is about.
        await factory.TruncateUsersAsync();

        var resp = await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "First User",
            email = "first@regression.test",
            password = "RegPass!12345"
        });

        resp.Should().NotBeNull();
        resp!.Success.Should().BeTrue(resp.Message);
        resp.Data!.User.Role.Should().Be("admin",
            "BUG-001 fix says fresh DB → first registration is the admin");
    }

    [Fact(DisplayName = "Subsequent users register as plain 'user'")]
    public async Task SubsequentUsersAreUsers()
    {
        // Ensure at least one user already exists (FirstUserBecomesAdmin may have created them
        // but xUnit doesn't guarantee order; create explicitly here).
        await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Seed Admin",
            email = "seed@regression.test",
            password = "RegPass!12345"
        });

        var resp = await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Second User",
            email = "second@regression.test",
            password = "RegPass!12345"
        });

        resp!.Data!.User.Role.Should().Be("user");
    }

    [Fact(DisplayName = "Login with correct password issues JWT + refresh token")]
    public async Task LoginIssuesTokens()
    {
        const string email = "login.test@regression.test";
        const string pwd = "RegPass!12345";

        await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Login Test",
            email,
            password = pwd
        });

        var login = await factory.PostJsonAsync<AuthResp>("/api/v1/auth/login", new { email, password = pwd });
        login!.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        login.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        login.Data.User.Email.Should().Be(email);
    }

    [Fact(DisplayName = "Login with wrong password returns 400/401")]
    public async Task LoginWrongPasswordRejected()
    {
        const string email = "wrongpwd@regression.test";
        await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Wrong Pwd Tester",
            email,
            password = "Correct!Pwd123"
        });

        var (status, _) = await factory.PostRawAsync("/api/v1/auth/login", new
        {
            email,
            password = "Definitely!Wrong"
        });

        ((int)status).Should().BeOneOf(400, 401);
    }

    [Fact(DisplayName = "Login with unknown email returns 400/401/404 (no info leak)")]
    public async Task UnknownEmailRejected()
    {
        var (status, _) = await factory.PostRawAsync("/api/v1/auth/login", new
        {
            email = "ghost@regression.test",
            password = "DoesNotMatter"
        });
        ((int)status).Should().BeOneOf(400, 401, 404);
    }

    [Fact(DisplayName = "Duplicate email registration rejected (409 or 400)")]
    public async Task DuplicateEmailRejected()
    {
        const string email = "dup@regression.test";
        await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Original",
            email,
            password = "RegPass!12345"
        });
        var (status, _) = await factory.PostRawAsync("/api/v1/auth/register", new
        {
            fullName = "Imposter",
            email,
            password = "DiffPass!23X"
        });
        ((int)status).Should().BeOneOf(400, 409);
    }

    [Fact(DisplayName = "Protected endpoint without JWT returns 401")]
    public async Task ProtectedEndpointRequiresJwt()
    {
        var (status, _) = await factory.GetRawAsync("/api/v1/contacts");
        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Tampered JWT rejected")]
    public async Task TamperedJwtRejected()
    {
        var (status, _) = await factory.GetRawAsync(
            "/api/v1/contacts",
            jwt: "eyJ.tampered.token123");
        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Refresh token returns new access token")]
    public async Task RefreshTokenWorks()
    {
        const string email = "refresh@regression.test";
        const string pwd = "RegPass!12345";

        await factory.PostJsonAsync<AuthResp>("/api/v1/auth/register", new
        {
            fullName = "Refresh Test",
            email,
            password = pwd
        });
        var login = await factory.PostJsonAsync<AuthResp>("/api/v1/auth/login", new { email, password = pwd });

        var refreshed = await factory.PostJsonAsync<AuthResp>("/api/v1/auth/refresh", new
        {
            refreshToken = login!.Data!.RefreshToken
        });

        refreshed!.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshed.Data.AccessToken.Should().NotBe(login.Data.AccessToken,
            "a fresh access token must differ from the original");
    }
}

// DTOs scoped to the test project — match the API's response shape but stay independent
// so internal API changes don't silently break the harness. The API wraps these in
// ApiEnvelope<T> automatically, so AuthResp is the INNER `data` shape only.
public record AuthResp(string AccessToken, string RefreshToken, UserSummary User);
public record UserSummary(Guid Id, string FullName, string Email, string Role);
