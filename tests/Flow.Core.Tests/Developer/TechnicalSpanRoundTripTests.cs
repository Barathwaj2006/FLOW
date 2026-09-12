using System;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Developer;

public class TechnicalSpanRoundTripTests
{
    [Theory]
    [InlineData(".NET 9")]
    [InlineData("C#")]
    [InlineData("C++")]
    [InlineData("Flow.Core.Context")]
    [InlineData("getUserProfileAsync")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData("--no-incremental")]
    [InlineData("MAX_RETRY_COUNT")]
    [InlineData("APIClientV2")]
    [InlineData("OAuth2Token")]
    [InlineData("IPv6Parser")]
    [InlineData("H264Decoder")]
    [InlineData("https://github.com/Barathwaj2006/FLOW")]
    [InlineData("%PATH%")]
    [InlineData("$env:USERPROFILE")]
    public void TechnicalSpan_RoundTrip_PreservedExactlyWithoutCorruption(string token)
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = $"We configured {token} for production deployment.";
        string output = pipeline.Format(input);

        Assert.Contains(token, output);
        Assert.DoesNotContain("__FLOW_TECH_", output);
        Assert.DoesNotContain("", output);
        Assert.DoesNotContain("", output);
    }

    [Fact]
    public void TechnicalSpan_MultipleSpansInSingleSentence_AllPreservedExactly()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = @"Build C# with .NET 9 using dotnet test --no-incremental at C:\Program Files\dotnet\dotnet.exe.";
        string output = pipeline.Format(input);

        Assert.Contains("C#", output);
        Assert.Contains(".NET 9", output);
        Assert.Contains("dotnet test", output);
        Assert.Contains("--no-incremental", output);
        Assert.Contains(@"C:\Program Files\dotnet\dotnet.exe", output);
        Assert.DoesNotContain("", output);
        Assert.DoesNotContain("", output);
    }
}
