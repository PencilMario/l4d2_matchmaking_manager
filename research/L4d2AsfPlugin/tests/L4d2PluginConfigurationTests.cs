using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2AsfPlugin.Tests;

[TestClass]
public sealed class L4d2PluginConfigurationTests
{
    [TestMethod]
    public void EmptyConfigurationDisablesAllBots()
    {
        var configuration = Parse("{}");

        Assert.IsFalse(IsEnabledFor(configuration, "sirp"));
    }

    [TestMethod]
    public void ValidConfigurationEnablesWhitelistedBotCaseInsensitively()
    {
        var configuration = Parse(
            """
            {
              "EnabledBots": ["SirP"],
              "Endpoint": "106.54.197.3:24561",
              "ReservationEnabled": false,
              "LobbyType": "Public",
              "MaxMembers": 8
            }
            """);

        Assert.IsTrue(IsEnabledFor(configuration, "sirp"));
        Assert.IsFalse(IsEnabledFor(configuration, "other-bot"));
    }

    [TestMethod]
    public void InvalidEndpointIsRejected()
    {
        var parsed = TryParse("""{"EnabledBots":["sirp"],"Endpoint":"example.org:24561"}""");

        Assert.IsFalse(parsed.Success);
        StringAssert.Contains(parsed.Error, "IPv4");
    }

    [TestMethod]
    public void InvalidMemberLimitIsRejected()
    {
        var parsed = TryParse(
            """{"EnabledBots":["sirp"],"Endpoint":"106.54.197.3:24561","MaxMembers":0}""");

        Assert.IsFalse(parsed.Success);
        StringAssert.Contains(parsed.Error, "MaxMembers");
    }

    [TestMethod]
    public void ConfigurationAssemblyDoesNotUseTrimmedSourceGenerationOptions()
    {
        var hasUnsupportedOption = typeof(LobbyMetadata).Assembly
            .DefinedTypes
            .SelectMany(static type => type.GetCustomAttributesData())
            .Where(static attribute => attribute.AttributeType.FullName ==
                "System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute")
            .SelectMany(static attribute => attribute.NamedArguments)
            .Any(static argument => argument.MemberName == "PropertyNameCaseInsensitive");

        Assert.IsFalse(hasUnsupportedOption);
    }

    private static object Parse(string json)
    {
        var parsed = TryParse(json);
        Assert.IsTrue(parsed.Success, parsed.Error);
        Assert.IsNotNull(parsed.Configuration);
        return parsed.Configuration;
    }

    private static (bool Success, object? Configuration, string Error) TryParse(string json)
    {
        var configurationType = typeof(LobbyMetadata).Assembly.GetType(
            "L4d2AsfPlugin.L4d2PluginConfiguration")
            ?? throw new AssertFailedException("L4d2PluginConfiguration must own plugin JSON validation.");
        var tryParse = configurationType.GetMethod(
            "TryParse",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("L4d2PluginConfiguration.TryParse must be available.");
        var arguments = new object?[] { json, null, null };
        var success = tryParse.Invoke(null, arguments);
        return ((bool)success!, arguments[1], (string?)arguments[2] ?? string.Empty);
    }

    private static bool IsEnabledFor(object configuration, string botName)
    {
        var method = configuration.GetType().GetMethod(
            "IsEnabledFor",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("L4d2PluginConfiguration.IsEnabledFor must be available.");
        return (bool)method.Invoke(configuration, [botName])!;
    }

}
