using System;
using System.Collections.Generic;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Developer;

/// <summary>
/// Property-Based Invariant Tests for Developer Mode.
/// Formally asserts mathematical properties:
/// 1. Casing Idempotence: Transform(Transform(x, Style), Style) == Transform(x, Style).
/// 2. Zero-Enter Invariance: Pipeline(input) NEVER contains \r, \n, 0x0D, or 0x0A.
/// 3. Technical Span Invariance: Protected technical tokens survive end-to-end without mutation.
/// 4. Path Invariance: Valid Windows paths normalize slashes, preserve drive letters, and reject trailing periods.
/// 5. Developer Identifier Purity: Code identifiers never contain internal placeholder artifacts.
/// </summary>
public class DeveloperPropertyTests
{
    private readonly TranscriptProcessingPipeline _pipeline = new();

    private static readonly FormattingOptions CodeOptions = new(
        Category: ApplicationCategory.Code,
        TargetApplication: "code",
        DeveloperContext: new DeveloperContext("code", ApplicationCategory.Code, LanguageCatalog.English, IdentifierCasingStyle.CamelCase, IsCodeEditor: true)
    );

    private static readonly FormattingOptions TerminalOptions = new(
        Category: ApplicationCategory.Terminal,
        TargetApplication: "windowsterminal",
        DeveloperContext: new DeveloperContext("windowsterminal", ApplicationCategory.Terminal, LanguageCatalog.English, IdentifierCasingStyle.None, IsTerminal: true)
    );

    private static readonly string[] SamplePhrases =
    [
        "user profile manager",
        "database connection pool",
        "http response handler",
        "api client v2",
        "max retry count",
        "default timeout seconds",
        "active session token",
        "get user by id",
        "validate token signature",
        "parse json response",
        "point 2d coordinate",
        "point 3d coordinate",
        "ipv6 parser handler",
        "oauth2 token provider"
    ];

    [Fact]
    public void Property1_CasingIdempotence_CamelCase_IsStrictlyIdempotent()
    {
        foreach (var phrase in SamplePhrases)
        {
            string once = CasingTransformer.ToCamelCase(phrase);
            string twice = CasingTransformer.ToCamelCase(once);
            string thrice = CasingTransformer.ToCamelCase(twice);

            Assert.Equal(once, twice);
            Assert.Equal(twice, thrice);
        }
    }

    [Fact]
    public void Property1_CasingIdempotence_PascalCase_IsStrictlyIdempotent()
    {
        foreach (var phrase in SamplePhrases)
        {
            string once = CasingTransformer.ToPascalCase(phrase);
            string twice = CasingTransformer.ToPascalCase(once);
            string thrice = CasingTransformer.ToPascalCase(twice);

            Assert.Equal(once, twice);
            Assert.Equal(twice, thrice);
        }
    }

    [Fact]
    public void Property1_CasingIdempotence_SnakeCase_IsStrictlyIdempotent()
    {
        foreach (var phrase in SamplePhrases)
        {
            string once = CasingTransformer.ToSnakeCase(phrase);
            string twice = CasingTransformer.ToSnakeCase(once);
            string thrice = CasingTransformer.ToSnakeCase(twice);

            Assert.Equal(once, twice);
            Assert.Equal(twice, thrice);
        }
    }

    [Fact]
    public void Property1_CasingIdempotence_KebabCase_IsStrictlyIdempotent()
    {
        foreach (var phrase in SamplePhrases)
        {
            string once = CasingTransformer.ToKebabCase(phrase);
            string twice = CasingTransformer.ToKebabCase(once);
            string thrice = CasingTransformer.ToKebabCase(twice);

            Assert.Equal(once, twice);
            Assert.Equal(twice, thrice);
        }
    }

    [Fact]
    public void Property1_CasingIdempotence_ConstantCase_IsStrictlyIdempotent()
    {
        foreach (var phrase in SamplePhrases)
        {
            string once = CasingTransformer.ToConstantCase(phrase);
            string twice = CasingTransformer.ToConstantCase(once);
            string thrice = CasingTransformer.ToConstantCase(twice);

            Assert.Equal(once, twice);
            Assert.Equal(twice, thrice);
        }
    }

    [Theory]
    [InlineData("function calculate total")]
    [InlineData("class user repository")]
    [InlineData("interface audio stream")]
    [InlineData("camel case session token")]
    [InlineData("c colon backslash windows backslash system32 backslash notepad dot exe")]
    [InlineData("git checkout -b feature/login")]
    [InlineData("x fat arrow x dot id")]
    [InlineData("status double equals active")]
    [InlineData("this is a regular sentence with some code like apiClientV2 embedded.")]
    public void Property2_ZeroEnterInvariance_HoldsAcrossAllContexts(string input)
    {
        string codeOutput = _pipeline.Format(input, CodeOptions);
        string termOutput = _pipeline.Format(input, TerminalOptions);
        string defaultOutput = _pipeline.Format(input);

        foreach (var output in new[] { codeOutput, termOutput, defaultOutput })
        {
            Assert.DoesNotContain("\r", output);
            Assert.DoesNotContain("\n", output);
            Assert.DoesNotContain("\u000D", output);
            Assert.DoesNotContain("\u000A", output);
        }
    }

    [Theory]
    [InlineData(".NET 9")]
    [InlineData("C# 12")]
    [InlineData("ASP.NET Core")]
    [InlineData("WinUI 3")]
    [InlineData("WASAPI")]
    [InlineData("DirectML")]
    [InlineData("ONNX Runtime")]
    [InlineData("SQLite")]
    [InlineData("VS Code")]
    [InlineData("TypeScript")]
    [InlineData("DPAPI")]
    [InlineData("APIClientV2")]
    [InlineData("OAuth2Token")]
    [InlineData("IPv6Parser")]
    public void Property3_TechnicalSpanInvariance_PreservesEntityExactly(string entity)
    {
        string input = $"The system integrates {entity} for high performance.";
        string output = _pipeline.Format(input, CodeOptions);

        Assert.Contains(entity, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
        Assert.DoesNotContain("\uE000", output);
        Assert.DoesNotContain("\uE001", output);
        Assert.DoesNotContain("__TECH_ENT_", output);
    }

    [Theory]
    [InlineData("c colon backslash windows backslash system32 backslash cmd dot exe", @"C:\windows\system32\cmd.exe")]
    [InlineData("c colon backslash program files backslash dotnet backslash dotnet dot exe", @"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData("c colon backslash users backslash dev backslash project", @"C:\Users\dev\project")]
    [InlineData("d colon backslash models backslash ggml tiny dot bin", @"D:\models\ggml_tiny.bin")]
    public void Property4_PathInvariance_NeverAppendsTrailingPeriod(string spoken, string expected)
    {
        string output = _pipeline.Format(spoken, CodeOptions);

        Assert.Equal(expected, output);
        Assert.False(output.EndsWith('.'), "Paths must never end with a period.");
    }

    [Theory]
    [InlineData("camel case user profile", "userProfile")]
    [InlineData("pascal case order manager", "OrderManager")]
    [InlineData("snake case table name", "table_name")]
    [InlineData("kebab case header bar", "header-bar")]
    [InlineData("constant case max retry", "MAX_RETRY")]
    public void Property5_SpokenCasing_ProducesPureIdentifiersWithoutArtifacts(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);

        Assert.Equal(expected, output);
        Assert.DoesNotContain(" ", output);
        Assert.DoesNotContain(".", output);
        Assert.DoesNotContain("\uE000", output);
        Assert.DoesNotContain("__TECH_ENT_", output);
    }
}
