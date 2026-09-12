using System;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.TranscriptProcessing;
using Flow.Core.TranscriptProcessing.Stages;
using Xunit;

namespace Flow.Core.Tests.Developer;

public class Phase6DeveloperModeTests
{
    private readonly TranscriptProcessingPipeline _pipeline = new();

    private static FormattingOptions CodeOptions => new(
        Category: ApplicationCategory.Code,
        TargetApplication: "code",
        DeveloperContext: new DeveloperContext("code", ApplicationCategory.Code, LanguageCatalog.English, IdentifierCasingStyle.CamelCase, IsCodeEditor: true)
    );

    private static FormattingOptions TerminalOptions => new(
        Category: ApplicationCategory.Terminal,
        TargetApplication: "windowsterminal",
        DeveloperContext: new DeveloperContext("windowsterminal", ApplicationCategory.Terminal, LanguageCatalog.English, IdentifierCasingStyle.None, IsTerminal: true)
    );

    private static FormattingOptions ProseOptions => new(
        Category: ApplicationCategory.GeneralProse,
        TargetApplication: "notepad",
        DeveloperContext: new DeveloperContext("notepad", ApplicationCategory.GeneralProse, LanguageCatalog.English, IdentifierCasingStyle.None)
    );

    // =========================================================================
    // 1. IDENTIFIER CASING TESTS (WF-032A)
    // =========================================================================

    [Theory]
    [InlineData("get user name", "getUserName")]
    [InlineData("user profile manager", "userProfileManager")]
    [InlineData("api client", "apiClient")]
    [InlineData("utf 8 encoder", "utf8Encoder")]
    [InlineData("is valid email address", "isValidEmailAddress")]
    public void CasingTransformer_ToCamelCase_TransformsAccurately(string input, string expected)
    {
        Assert.Equal(expected, CasingTransformer.ToCamelCase(input));
    }

    [Theory]
    [InlineData("user profile manager", "UserProfileManager")]
    [InlineData("database repository", "DatabaseRepository")]
    [InlineData("api client", "ApiClient")]
    [InlineData("http server", "HttpServer")]
    [InlineData("json parser", "JsonParser")]
    public void CasingTransformer_ToPascalCase_PreservesAcronyms(string input, string expected)
    {
        Assert.Equal(expected, CasingTransformer.ToPascalCase(input));
    }

    [Theory]
    [InlineData("get user name", "get_user_name")]
    [InlineData("user profile manager", "user_profile_manager")]
    [InlineData("api secret key", "api_secret_key")]
    [InlineData("max retry count", "max_retry_count")]
    public void CasingTransformer_ToSnakeCase_TransformsAccurately(string input, string expected)
    {
        Assert.Equal(expected, CasingTransformer.ToSnakeCase(input));
    }

    [Theory]
    [InlineData("max retry count", "MAX_RETRY_COUNT")]
    [InlineData("default timeout ms", "DEFAULT_TIMEOUT_MS")]
    [InlineData("api secret key", "API_SECRET_KEY")]
    public void CasingTransformer_ToScreamingSnakeCase_TransformsAccurately(string input, string expected)
    {
        Assert.Equal(expected, CasingTransformer.ToScreamingSnakeCase(input));
        Assert.Equal(expected, CasingTransformer.ToConstantCase(input));
    }

    [Theory]
    [InlineData("user profile manager", "user-profile-manager")]
    [InlineData("get user name", "get-user-name")]
    [InlineData("primary button component", "primary-button-component")]
    public void CasingTransformer_ToKebabCase_TransformsAccurately(string input, string expected)
    {
        Assert.Equal(expected, CasingTransformer.ToKebabCase(input));
    }

    [Fact]
    public void CasingTransformer_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CasingTransformer.ToCamelCase(""));
        Assert.Equal(string.Empty, CasingTransformer.ToPascalCase("   "));
        Assert.Equal(string.Empty, CasingTransformer.ToSnakeCase(null!));
    }

    // =========================================================================
    // 2. SPOKEN CASING TRIGGERS (WF-032B)
    // =========================================================================

    [Theory]
    [InlineData("camel case user profile manager", "userProfileManager")]
    [InlineData("snake case user profile manager", "user_profile_manager")]
    [InlineData("pascal case user profile manager", "UserProfileManager")]
    [InlineData("kebab case user profile manager", "user-profile-manager")]
    [InlineData("screaming snake case max retry count", "MAX_RETRY_COUNT")]
    [InlineData("constant case max retry count", "MAX_RETRY_COUNT")]
    public void SpokenCasing_ExplicitTriggers_TransformCorrectly(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void SpokenCasing_EmbeddedInProse_TransformsOnlyTargetPhrase()
    {
        string input = "please create camel case user profile manager and return it";
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal("Please create userProfileManager and return it.", output);
    }

    // =========================================================================
    // 3. FUNCTION RECOGNITION TESTS
    // =========================================================================

    [Theory]
    [InlineData("function get user profile", "getUserProfile")]
    [InlineData("function get user profile async", "getUserProfileAsync")]
    [InlineData("function create user session", "createUserSession")]
    [InlineData("function load configuration", "loadConfiguration")]
    [InlineData("method calculate total sum", "calculateTotalSum")]
    public void FunctionRecognition_TransformsToCamelCase(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void FunctionRecognition_AsyncPrefix_PreservesAsyncPrefix()
    {
        string input = "async function fetch remote data";
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal("async fetchRemoteData", output);
    }

    // =========================================================================
    // 4. CLASS & INTERFACE RECOGNITION TESTS
    // =========================================================================

    [Theory]
    [InlineData("class user profile service", "UserProfileService")]
    [InlineData("class database repository", "DatabaseRepository")]
    [InlineData("class API client", "ApiClient")]
    [InlineData("class HTTP server", "HttpServer")]
    [InlineData("struct point 2d", "Point2D")]
    [InlineData("enum user status", "UserStatus")]
    public void ClassRecognition_TransformsToPascalCase(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("interface user repository", "IUserRepository")]
    [InlineData("interface I user repository", "IUserRepository")]
    [InlineData("interface order service", "IOrderService")]
    public void InterfaceRecognition_EnsuresIPrefix(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
    }

    // =========================================================================
    // 5. PATH & FILE RECOGNITION (WF-034)
    // =========================================================================

    [Theory]
    [InlineData("at app dot ts", "@app.ts")]
    [InlineData("at user underscore profile dot cs", "@user_profile.cs")]
    [InlineData("at config dot json", "@config.json")]
    [InlineData("at src slash flow dot cs", "@src/flow.cs")]
    public void VoiceFileTagging_FormatsTagSyntax(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void SpokenWindowsPath_ConvertsToValidPath()
    {
        string input = "c colon backslash users backslash barathwaj backslash desktop backslash flow";
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(@"C:\Users\barathwaj\desktop\flow", output);
    }

    [Theory]
    [InlineData(@"C:\Users\barathwaj\Desktop\FLOW\src")]
    [InlineData(@"src/Flow.Core/Context")]
    [InlineData("Flow.Core.csproj")]
    [InlineData("appsettings.json")]
    public void PathRecognition_PreservesExistingPathsAndExtensions(string path)
    {
        string output = _pipeline.Format($"inspect {path} now", CodeOptions);
        Assert.Contains(path, output);
    }

    // =========================================================================
    // 6. CODE PUNCTUATION IN CODE CONTEXT
    // =========================================================================

    [Theory]
    [InlineData("items dot map item fat arrow item dot id", "items.map item => item.id")]
    [InlineData("pointer thin arrow value", "pointer -> value")]
    [InlineData("if a double equals b", "if a == b")]
    [InlineData("if a not equals b", "if a != b")]
    [InlineData("const x equals 42", "const x = 42")]
    [InlineData("wrap in backtick code backtick", "Wrap in `code`")]
    public void CodePunctuation_InCodeContext_TransformsAccurately(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
    }

    // =========================================================================
    // 7. CONSERVATIVE PROSE ISOLATION (NON-REGRESSION)
    // =========================================================================

    [Theory]
    [InlineData("the function of this department is crucial", "The function of this department is crucial.")]
    [InlineData("a class of thirty students attended", "A class of thirty students attended.")]
    [InlineData("we booked a first class flight to Seattle", "We booked a first class flight to Seattle.")]
    [InlineData("the meeting was held in the conference room", "The meeting was held in the conference room.")]
    [InlineData("an arrow pointing towards the entrance", "An arrow pointing towards the entrance.")]
    public void ProseContext_PreservesOrdinaryEnglishWithoutCodeMangle(string input, string expected)
    {
        string output = _pipeline.Format(input, ProseOptions);
        Assert.Equal(expected, output);
    }

    // =========================================================================
    // 8. TECHNICAL ENTITY PROTECTION (WF-033)
    // =========================================================================

    [Theory]
    [InlineData("we are upgrading to .NET 9 and C# 12", ".NET")]
    [InlineData("implemented using ASP.NET Core and WinUI 3", "ASP.NET")]
    [InlineData("local inference uses WASAPI and Whisper", "WASAPI")]
    [InlineData("database persistence is powered by SQLite", "SQLite")]
    [InlineData("frontend written in TypeScript with Next.js", "TypeScript")]
    [InlineData("containerized with Docker and deployed to Kubernetes", "Docker")]
    public void TechnicalEntityProtection_ProtectsFrameworksAndLanguages(string input, string expectedEntity)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Contains(expectedEntity, output);
        Assert.False(output.Contains('\r'));
        Assert.False(output.Contains('\n'));
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("git checkout master")]
    [InlineData("dotnet build --no-incremental")]
    [InlineData("dotnet test")]
    [InlineData("npm install --force")]
    [InlineData("docker compose up")]
    [InlineData("kubectl apply -f deployment.yaml")]
    public void TechnicalEntityProtection_ProtectsCliCommandsAndFlags(string command)
    {
        string output = _pipeline.Format(command, TerminalOptions);
        Assert.Equal(command, output);
    }

    // =========================================================================
    // 9. MULTILINGUAL DEVELOPER VOCABULARY COMPATIBILITY
    // =========================================================================

    [Fact]
    public void Multilingual_TamilSpeechWithEnglishIdentifier_PreservesIdentifier()
    {
        // Code-switched Indic suffix -ஐ on English identifier
        string input = "இந்த userProfileManager ஐ create பண்ணுங்க";
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Contains("userProfileManager", output);
        Assert.Contains("பண்ணுங்க", output);
    }

    [Fact]
    public void Multilingual_HindiSpeechWithEnglishIdentifier_PreservesIdentifier()
    {
        // Code-switched Hindi speech with English class
        string input = "इस DatabaseRepository को call करो";
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Contains("DatabaseRepository", output);
        Assert.Contains("करो", output);
    }

    // =========================================================================
    // 10. TERMINAL SAFETY & ZERO-ENTER INVARIANT
    // =========================================================================

    [Theory]
    [InlineData("git status")]
    [InlineData("git checkout -b feature/phase-6")]
    [InlineData("shutdown /s /t 0")]
    [InlineData("format c:")]
    [InlineData("rm -rf /")]
    public void TerminalContext_InsertsTextOnly_ZeroExecution_ZeroEnter(string cliText)
    {
        string output = _pipeline.Format(cliText, TerminalOptions);

        // Absolute fail-closed check: No \r, no \n
        Assert.False(output.Contains('\r'), "Terminal output must not contain carriage return");
        Assert.False(output.Contains('\n'), "Terminal output must not contain newline");

        // The text is purely returned as string for text insertion
        Assert.Contains(cliText.Split(' ')[0], output);
    }

    [Fact]
    public void DeveloperPipeline_GuaranteesZeroEnterOnMultiLineInput()
    {
        string input = "function get user profile\r\nclass user repository\r\ngit status";
        string output = _pipeline.Format(input, CodeOptions);

        Assert.False(output.Contains('\r'), "Output contained \\r carriage return!");
        Assert.False(output.Contains('\n'), "Output contained \\n newline!");
        Assert.Contains("getUserProfile", output);
        Assert.Contains("UserRepository", output);
    }
}
