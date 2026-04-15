using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.Tests;

public class AuthTokenTests
{
    [Fact]
    public void IsExpired_ReturnsFalse_WhenTokenIsFresh()
    {
        AuthToken token = new AuthToken
        {
            IdToken = "test-id-token",
            RefreshToken = "test-refresh-token",
            ExpiresIn = 3600,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        Assert.False(token.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenTokenHasExpired()
    {
        AuthToken token = new AuthToken
        {
            IdToken = "test-id-token",
            RefreshToken = "test-refresh-token",
            ExpiresIn = 3600,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };

        Assert.True(token.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenWithin60SecondSafetyMargin()
    {
        AuthToken token = new AuthToken
        {
            IdToken = "test-id-token",
            RefreshToken = "test-refresh-token",
            ExpiresIn = 3600,
            ExpiresAt = DateTime.UtcNow.AddSeconds(30) // 30s left, but safety margin is 60s
        };

        Assert.True(token.IsExpired);
    }

    [Fact]
    public void DefaultValues_AreEmptyStrings()
    {
        AuthToken token = new AuthToken();

        Assert.Equal(string.Empty, token.IdToken);
        Assert.Equal(string.Empty, token.RefreshToken);
        Assert.Equal(0, token.ExpiresIn);
    }
}

