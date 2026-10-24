using LexVerse.Infrastructure.Configuration;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class PlansWebsiteTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/pricing")]
    [InlineData("not a url")]
    [InlineData("http://example.test/plans")]
    [InlineData("file:///C:/plans.html")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.test/plans")]
    public void InvalidConfigurationNeverLaunches(string? value)
    {
        var website = new PlansWebsite(value);
        var launched = false;
        Assert.False(website.IsAvailable);
        Assert.Throws<InvalidOperationException>(() => website.Open(_ => launched = true));
        Assert.False(launched);
    }

    [Fact]
    public void OpensConfiguredPublicUrlWithoutAddingAccountData()
    {
        const string url = "https://example.test/plans?lang=vi#compare";
        var website = new PlansWebsite(url);
        Uri? opened = null;
        Assert.True(website.IsAvailable);
        website.Open(uri => opened = uri);
        Assert.Equal(url, opened?.AbsoluteUri);
    }

    [Fact]
    public void LaunchFailureCanBeRetried()
    {
        var website = new PlansWebsite("https://example.test/plans");
        Assert.Throws<System.ComponentModel.Win32Exception>(() =>
            website.Open(_ => throw new System.ComponentModel.Win32Exception()));
        var launched = false;
        website.Open(_ => launched = true);
        Assert.True(launched);
    }
}
