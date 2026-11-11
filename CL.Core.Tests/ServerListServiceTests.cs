using System.Linq;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests;

public class ServerListServiceTests
{
    private const string SampleJson = @"[
  {
    ""con"": ""play.paradise-craft.com"",
    ""ver"": ""1.21.11"",
    ""onl"": 42,
    ""url"": ""allmc.in.ua/play-paradise-craft-com"",
    ""ico"": ""cdn.allmc.in.ua/favicons/play-paradise-craft-com.png?0438a67d423e58f8a5965a997d1c91e8"",
    ""web"": ""wiki.paradise-craft.com/s/wiki/doc/yak-zajti-na-server-jfcI1G9UdT"",
    ""dis"": ""discord.gg/paradisecraft""
  },
  {
    ""con"": ""45.143.198.54:25592"",
    ""ver"": ""1.20.4"",
    ""onl"": 10,
    ""url"": ""allmc.in.ua/custom-port-server"",
    ""ico"": ""cdn.allmc.in.ua/favicons/custom.png"",
    ""web"": null,
    ""dis"": null
  }
]";

    [Fact]
    public void ParseServersJson_ParsesAllMcFormatCorrectly()
    {
        var servers = ServerListService.ParseServersJson(SampleJson);

        Assert.NotNull(servers);
        Assert.Equal(2, servers.Count);

        var first = servers[0];
        Assert.Equal("play.paradise-craft.com", first.Name);
        Assert.Equal("play.paradise-craft.com", first.Ip);
        Assert.Equal(25565, first.Port);
        Assert.Equal("1.21.11", first.Version);
        Assert.Equal(42, first.OnlinePlayers);
        Assert.Equal("https://cdn.allmc.in.ua/favicons/play-paradise-craft-com.png?0438a67d423e58f8a5965a997d1c91e8", first.LogoUrl);
        Assert.Equal("https://wiki.paradise-craft.com/s/wiki/doc/yak-zajti-na-server-jfcI1G9UdT", first.SiteLink);
        Assert.Equal("https://discord.gg/paradisecraft", first.DiscordLink);
        Assert.Equal("https://allmc.in.ua/play-paradise-craft-com", first.AllMcUrl);

        var second = servers[1];
        Assert.Equal("45.143.198.54:25592", second.Name);
        Assert.Equal("45.143.198.54", second.Ip);
        Assert.Equal(25592, second.Port);
        Assert.Equal("1.20.4", second.Version);
        Assert.Equal(10, second.OnlinePlayers);
        Assert.Equal(string.Empty, second.SiteLink);
        Assert.Equal(string.Empty, second.DiscordLink);
        Assert.Equal("https://allmc.in.ua/custom-port-server", second.AllMcUrl);
    }

    [Fact]
    public void ParseServersJson_FiltersBySearchQuery()
    {
        var filteredByVersion = ServerListService.ParseServersJson(SampleJson, "1.20.4");
        Assert.Single(filteredByVersion);
        Assert.Equal("45.143.198.54", filteredByVersion[0].Ip);

        var filteredByName = ServerListService.ParseServersJson(SampleJson, "paradise");
        Assert.Single(filteredByName);
        Assert.Equal("play.paradise-craft.com", filteredByName[0].Name);

        var filteredNoMatch = ServerListService.ParseServersJson(SampleJson, "nonexistent");
        Assert.Empty(filteredNoMatch);
    }

    [Fact]
    public void ParseServersJson_HandlesInvalidOrEmptyJsonGracefully()
    {
        Assert.Empty(ServerListService.ParseServersJson(string.Empty));
        Assert.Empty(ServerListService.ParseServersJson("   "));
        Assert.Empty(ServerListService.ParseServersJson(null!));
        Assert.Empty(ServerListService.ParseServersJson("invalid json content"));
        Assert.Empty(ServerListService.ParseServersJson(@"{""some"": ""object""}"));
    }

    [Theory]
    [InlineData("allmc.in.ua", "https://allmc.in.ua")]
    [InlineData("https://allmc.in.ua", "https://allmc.in.ua")]
    [InlineData("http://allmc.in.ua", "http://allmc.in.ua")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void EnsureHttps_HandlesVariationsCorrectly(string? input, string expected)
    {
        string result = ServerListService.EnsureHttps(input);
        Assert.Equal(expected, result);
    }
}
