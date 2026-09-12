using System;
using System.Collections.Generic;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

/// <summary>
/// Phase 7.5 Adversarial Audit: Safe Transform Correctness, Idempotence, Technical Token Preservation, Code Safety, and Multilingual Commands.
/// Covers Master Audit Sections 19, 20, 21, 22, 23.
/// </summary>
public class Phase75TransformAndMultilingualTests
{
    private readonly DeterministicTextTransformEngine _engine = new();
    private readonly DeterministicCommandParser _parser = new();

    #region Section 19: Golden Expected Output Tests for All 19 Transforms

    [Theory]
    // 1. Bullet List
    [InlineData(TransformType.BulletList, "apple, banana, cherry", "• Apple • Banana • Cherry")]
    [InlineData(TransformType.BulletList, "first item\nsecond item\nthird item", "• First item • Second item • Third item")]
    // 2. Numbered List
    [InlineData(TransformType.NumberedList, "apple, banana, cherry", "1. Apple 2. Banana 3. Cherry")]
    [InlineData(TransformType.NumberedList, "one\ntwo\nthree", "1. One 2. Two 3. Three")]
    // 3. Uppercase
    [InlineData(TransformType.Uppercase, "hello world", "HELLO WORLD")]
    // 4. Lowercase
    [InlineData(TransformType.Lowercase, "HELLO WORLD", "hello world")]
    // 5. Title Case
    [InlineData(TransformType.TitleCase, "the quick brown fox", "The Quick Brown Fox")]
    // 6. Camel Case
    [InlineData(TransformType.CamelCase, "user profile service", "userProfileService")]
    // 7. Snake Case
    [InlineData(TransformType.SnakeCase, "user profile service", "user_profile_service")]
    // 8. Pascal Case
    [InlineData(TransformType.PascalCase, "user profile service", "UserProfileService")]
    // 9. Kebab Case
    [InlineData(TransformType.KebabCase, "user profile service", "user-profile-service")]
    // 10. Wrap in Quotes
    [InlineData(TransformType.WrapQuotes, "special token", "\"special token\"")]
    // 11. Wrap in Backticks
    [InlineData(TransformType.WrapBackticks, "git status", "`git status`")]
    // 12. Wrap in Code Block
    [InlineData(TransformType.WrapCodeBlock, "int x = 42;", "``` int x = 42; ```")]
    // 13. Trim Whitespace
    [InlineData(TransformType.TrimWhitespace, "   hello    world   ", "hello world")]
    // 14. Make Concise
    [InlineData(TransformType.MakeConcise, "um basically like we need to test this actually", "We need to test this")]
    // 15. Make Formal
    [InlineData(TransformType.MakeFormal, "we can't and won't do it because they're not ready", "we cannot and will not do it because they are not ready")]
    // 16. Fix Whitespace
    [InlineData(TransformType.FixWhitespace, "line one\r\n   line two\n\nline three", "line one line two line three")]
    // 17. Fix Punctuation
    [InlineData(TransformType.FixPunctuation, "hello , world .how are you ?", "Hello, world. How are you?")]
    // 18. Normalize Spacing
    [InlineData(TransformType.NormalizeSpacing, "call( arg1 , arg2 ) ;", "call(arg1, arg2);")]
    // 19. Normalize Quotes
    [InlineData(TransformType.NormalizeQuotes, "“smart double” and ‘smart single’", "\"smart double\" and 'smart single'")]
    public void SafeTransforms_GoldenExpectedOutput(TransformType transform, string input, string expected)
    {
        string actual = _engine.Transform(input, transform);
        Assert.Equal(expected, actual);
    }

    #endregion

    #region Section 20: Transform Idempotence T(T(x)) == T(x)

    public static IEnumerable<object[]> GetIdempotenceTestVectors()
    {
        var transforms = new[]
        {
            TransformType.BulletList,
            TransformType.NumberedList,
            TransformType.Uppercase,
            TransformType.Lowercase,
            TransformType.TitleCase,
            TransformType.CamelCase,
            TransformType.SnakeCase,
            TransformType.PascalCase,
            TransformType.KebabCase,
            TransformType.WrapQuotes,
            TransformType.WrapBackticks,
            TransformType.WrapCodeBlock,
            TransformType.TrimWhitespace,
            TransformType.MakeConcise,
            TransformType.MakeFormal,
            TransformType.FixWhitespace,
            TransformType.FixPunctuation,
            TransformType.NormalizeSpacing,
            TransformType.NormalizeQuotes
        };

        var samples = new[]
        {
            "short text",
            "Single",
            "hello world test",
            "APIClientV2",
            "https://localhost:5001",
            "C:\\dev\\project",
            "int value = 42;",
            "வணக்கம் உலகம்", // Tamil Unicode
            "नमस्ते दुनिया",   // Hindi Unicode
            "   padded   spaces   "
        };

        foreach (var t in transforms)
        {
            foreach (var s in samples)
            {
                yield return new object[] { t, s };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetIdempotenceTestVectors))]
    public void SafeTransforms_IdempotenceVerified(TransformType transform, string sample)
    {
        string once = _engine.Transform(sample, transform);
        string twice = _engine.Transform(once, transform);

        Assert.Equal(once, twice);
    }

    #endregion

    #region Section 21: Technical Token Preservation

    public static readonly string[] TechnicalCorpus = new string[]
    {
        "APIClientV2",
        "HTTP2Client",
        "OAuth2Token",
        "XMLHttpRequest",
        "IHTTPRequestHandler",
        "IPAddress",
        "UUIDParser",
        "JSONSerializer",
        "userProfileService",
        "my_function",
        "PascalCase",
        "snake_case",
        "kebab-case",
        "SCREAMING_SNAKE_CASE",
        "C:\\Users\\barat\\project",
        "./src/main.cs",
        "@filename",
        "https://example.com/api/v2"
    };

    [Theory]
    [InlineData(TransformType.WrapQuotes)]
    [InlineData(TransformType.WrapBackticks)]
    [InlineData(TransformType.WrapCodeBlock)]
    [InlineData(TransformType.TrimWhitespace)]
    [InlineData(TransformType.FixWhitespace)]
    public void SafeTransforms_FormattingTransforms_PreserveTechnicalTokensExactly(TransformType transform)
    {
        foreach (var token in TechnicalCorpus)
        {
            string transformed = _engine.Transform(token, transform);
            // Verify that the exact token characters remain preserved inside formatting
            Assert.Contains(token.Trim(), transformed, StringComparison.Ordinal);
        }
    }

    #endregion

    #region Section 22: Code Transform Safety

    [Fact]
    public void SafeTransforms_CodeSnippets_BracesAndGenericsPreserved()
    {
        string codeSnippet = "public async Task<List<string>> GetDataAsync(int id) { return new List<string>(); }";

        string inBackticks = _engine.Transform(codeSnippet, TransformType.WrapBackticks);
        Assert.Equal($"`{codeSnippet}`", inBackticks);

        string inCodeBlock = _engine.Transform(codeSnippet, TransformType.WrapCodeBlock);
        Assert.Equal($"``` {codeSnippet} ```", inCodeBlock);

        string whitespaceFixed = _engine.Transform(codeSnippet, TransformType.FixWhitespace);
        Assert.Equal(codeSnippet, whitespaceFixed);
    }

    #endregion

    #region Section 23: Multilingual Command Audit (English, Tamil, Hindi)

    [Theory]
    // English undo
    [InlineData("undo", true, "undo")]
    [InlineData("undo that", true, "undo")]
    [InlineData("please undo", true, "undo")]
    [InlineData("undo please", true, "undo")]
    [InlineData("revert", true, "undo")]
    // Tamil undo
    [InlineData("ரத்து செய்", true, "undo")]
    [InlineData("தயவுசெய்து ரத்து செய்", true, "undo")]
    // Hindi undo
    [InlineData("पूर्ववत करो", true, "undo")]
    [InlineData("कृपया पूर्ववत करो", true, "undo")]
    // Unsupported / ambiguous multilingual phrases fail closed
    [InlineData("ரத்து", false, null)]
    [InlineData("செய்", false, null)]
    [InlineData("தயவுசெய்து", false, null)]
    [InlineData("पूर्ववत", false, null)]
    [InlineData("करो", false, null)]
    [InlineData("कृपया", false, null)]
    [InlineData("வணக்கம்", false, null)]
    [InlineData("नमस्ते", false, null)]
    public void MultilingualCommands_Audit(string utterance, bool shouldRecognize, string? expectedAction)
    {
        var intent = _parser.Parse(utterance);

        if (shouldRecognize)
        {
            Assert.IsType<EditorCommandIntent>(intent);
            var editorIntent = (EditorCommandIntent)intent;
            Assert.Equal(expectedAction, editorIntent.ActionName);
        }
        else
        {
            Assert.IsType<UnknownCommandIntent>(intent);
        }
    }

    #endregion
}
