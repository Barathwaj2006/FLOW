using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class SafeTransformTests
{
    private readonly DeterministicTextTransformEngine _engine = new();

    [Fact]
    public void Transform_BulletList_FormatsCorrectly()
    {
        string input = "apples, bananas, oranges";
        string output = _engine.Transform(input, TransformType.BulletList);
        Assert.Equal("• Apples • Bananas • Oranges", output);
    }

    [Fact]
    public void Transform_NumberedList_FormatsCorrectly()
    {
        string input = "first, second, third";
        string output = _engine.Transform(input, TransformType.NumberedList);
        Assert.Equal("1. First 2. Second 3. Third", output);
    }

    [Fact]
    public void Transform_CasingTransforms_WorkAccurately()
    {
        string input = "user profile service";
        Assert.Equal("USER PROFILE SERVICE", _engine.Transform(input, TransformType.Uppercase));
        Assert.Equal("user profile service", _engine.Transform(input, TransformType.Lowercase));
        Assert.Equal("User Profile Service", _engine.Transform(input, TransformType.TitleCase));
        Assert.Equal("userProfileService", _engine.Transform(input, TransformType.CamelCase));
        Assert.Equal("user_profile_service", _engine.Transform(input, TransformType.SnakeCase));
        Assert.Equal("UserProfileService", _engine.Transform(input, TransformType.PascalCase));
        Assert.Equal("user-profile-service", _engine.Transform(input, TransformType.KebabCase));
    }

    [Fact]
    public void Transform_WrappingTransforms_WrapAccurately()
    {
        string input = "const x = 42";
        Assert.Equal("\"const x = 42\"", _engine.Transform(input, TransformType.WrapQuotes));
        Assert.Equal("`const x = 42`", _engine.Transform(input, TransformType.WrapBackticks));
        Assert.Contains("```", _engine.Transform(input, TransformType.WrapCodeBlock));
    }

    [Fact]
    public void Transform_WhitespaceAndContractions_HandledDeterministically()
    {
        Assert.Equal("hello world", _engine.Transform("   hello    world   ", TransformType.TrimWhitespace));
        Assert.Equal("cannot do it", _engine.Transform("can't do it", TransformType.MakeFormal));
        Assert.Equal("We need", _engine.Transform("basically like um we need", TransformType.MakeConcise));
    }

    [Fact]
    public void Transform_NewPhase7Transforms_ExecuteCleanly()
    {
        // FixWhitespace
        Assert.Equal("hello world", _engine.Transform("hello \r\n   world", TransformType.FixWhitespace));

        // FixPunctuation
        Assert.Equal("Hello, world. Test", _engine.Transform("hello , world . test", TransformType.FixPunctuation));

        // NormalizeSpacing
        Assert.Equal("(test), ok", _engine.Transform("( test ) , ok", TransformType.NormalizeSpacing));

        // NormalizeQuotes
        Assert.Equal("\"smart\" 'single'", _engine.Transform("“smart” ‘single’", TransformType.NormalizeQuotes));
    }

    [Fact]
    public void TransformSafetyValidator_CategorizesSizesAccurately()
    {
        Assert.Equal(SelectionSizeCategory.Small, TransformSafetyValidator.CategorizeSelection("short text"));
        Assert.Equal(SelectionSizeCategory.Medium, TransformSafetyValidator.CategorizeSelection(new string('a', 5000)));
        Assert.Equal(SelectionSizeCategory.Large, TransformSafetyValidator.CategorizeSelection(new string('a', 50000)));
        Assert.Equal(SelectionSizeCategory.Extreme, TransformSafetyValidator.CategorizeSelection(new string('a', 150000)));
    }

    [Fact]
    public void TransformSafetyValidator_AssignsRisksAccurately()
    {
        Assert.Equal(CommandRisk.Safe, TransformSafetyValidator.EvaluateTransformRisk(TransformType.Uppercase, "short"));
        Assert.Equal(CommandRisk.ConfirmRequired, TransformSafetyValidator.EvaluateTransformRisk(TransformType.Uppercase, new string('a', 50000)));
        Assert.Equal(CommandRisk.Blocked, TransformSafetyValidator.EvaluateTransformRisk(TransformType.Uppercase, new string('a', 150000)));
    }
}
