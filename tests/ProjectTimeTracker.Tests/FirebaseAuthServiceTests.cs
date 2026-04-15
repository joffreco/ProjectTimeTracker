using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ProjectTimeTracker.Configuration;
using ProjectTimeTracker.Services;

namespace ProjectTimeTracker.Tests;

public class FirebaseAuthServiceTests
{
    private readonly ILogger<FirebaseAuthService> _logger;
    private readonly IOptions<FirebaseAuthOptions> _options;

    public FirebaseAuthServiceTests()
    {
        _logger = Substitute.For<ILogger<FirebaseAuthService>>();
        _options = Options.Create(new FirebaseAuthOptions
        {
            ApiKey = "test-api-key",
            Email = "test@example.com",
            Password = "test-password"
        });
    }

    [Fact]
    public async Task SignInAsync_Success_ReturnsToken()
    {
        // Arrange — fake HTTP response
        string responseJson = JsonSerializer.Serialize(new
        {
            idToken = "fake-id-token",
            refreshToken = "fake-refresh-token",
            expiresIn = "3600",
            localId = "uid123",
            email = "test@example.com"
        });

        HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseJson);
        FirebaseAuthService sut = new FirebaseAuthService(httpClient, _options, _logger);

        // Act
        Domain.AuthToken token = await sut.SignInAsync("test@example.com", "test-password");

        // Assert
        Assert.Equal("fake-id-token", token.IdToken);
        Assert.Equal("fake-refresh-token", token.RefreshToken);
        Assert.Equal(3600, token.ExpiresIn);
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
        Assert.True(sut.IsAuthenticated);
        Assert.Equal("fake-id-token", sut.GetIdToken());
    }

    [Fact]
    public async Task SignInAsync_Failure_ThrowsException()
    {
        string errorJson = "{\"error\":{\"code\":400,\"message\":\"INVALID_PASSWORD\"}}";
        HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, errorJson);
        FirebaseAuthService sut = new FirebaseAuthService(httpClient, _options, _logger);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SignInAsync("test@example.com", "wrong-password"));

        Assert.False(sut.IsAuthenticated);
        Assert.Null(sut.GetIdToken());
    }

    [Fact]
    public async Task RefreshTokenAsync_Success_UpdatesToken()
    {
        // First sign in
        string signInJson = JsonSerializer.Serialize(new
        {
            idToken = "old-token",
            refreshToken = "refresh-token-1",
            expiresIn = "3600"
        });

        string refreshJson = JsonSerializer.Serialize(new
        {
            id_token = "new-token",
            refresh_token = "refresh-token-2",
            expires_in = "3600",
            token_type = "Bearer"
        });

        // We need two different responses — use a handler that tracks call count
        int callCount = 0;
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(signInJson)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(refreshJson)
            };
        });

        HttpClient httpClient = new HttpClient(handler);
        FirebaseAuthService sut = new FirebaseAuthService(httpClient, _options, _logger);

        await sut.SignInAsync("test@example.com", "test-password");
        Domain.AuthToken refreshed = await sut.RefreshTokenAsync("refresh-token-1");

        Assert.Equal("new-token", refreshed.IdToken);
        Assert.Equal("new-token", sut.GetIdToken());
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenNotSignedIn()
    {
        HttpClient httpClient = new HttpClient();
        FirebaseAuthService sut = new FirebaseAuthService(httpClient, _options, _logger);

        Assert.False(sut.IsAuthenticated);
        Assert.Null(sut.GetIdToken());
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        return new HttpClient(handler);
    }

    /// <summary>
    /// Simple fake handler that delegates to a Func for each request.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}

