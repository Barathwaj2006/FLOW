using System;
using System.Collections.Generic;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Developer;

/// <summary>
/// Dedicated Adversarial Developer Corpus Test Suite (Section 22).
/// Contains 107 curated developer-oriented cases covering functions, classes, interfaces,
/// structs, records, enums, casing triggers, code punctuation, file paths, @file tags,
/// CLI commands, CLI flags, frameworks, infrastructure, acronyms, and multilingual Indic developer speech.
/// Asserts ZERO corruption and 100% Zero-Enter compliance.
/// </summary>
public class AdversarialDeveloperCorpusTests
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

    // =========================================================================
    // 1. FUNCTIONS & METHODS (10 cases)
    // =========================================================================
    [Theory]
    [InlineData("function get user profile", "getUserProfile")]
    [InlineData("function get user profile async", "getUserProfileAsync")]
    [InlineData("async function load configuration", "async loadConfiguration")]
    [InlineData("function calculate quarterly tax", "calculateQuarterlyTax")]
    [InlineData("function validate token signature", "validateTokenSignature")]
    [InlineData("method create session token", "createSessionToken")]
    [InlineData("method parse json response", "parseJsonResponse")]
    [InlineData("function fetch remote data async", "fetchRemoteDataAsync")]
    [InlineData("function disconnect audio stream", "disconnectAudioStream")]
    [InlineData("method serialize object tree", "serializeObjectTree")]
    public void FunctionsAndMethods_TransformAccurately(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 2. CLASSES, STRUCTS, RECORDS, ENUMS, INTERFACES (12 cases)
    // =========================================================================
    [Theory]
    [InlineData("class user profile service", "UserProfileService")]
    [InlineData("class database repository", "DatabaseRepository")]
    [InlineData("class authentication middleware", "AuthenticationMiddleware")]
    [InlineData("struct point 2d", "Point2d")]
    [InlineData("struct vertex buffer", "VertexBuffer")]
    [InlineData("record user credentials", "UserCredentials")]
    [InlineData("record invoice summary", "InvoiceSummary")]
    [InlineData("enum payment status", "PaymentStatus")]
    [InlineData("enum execution state", "ExecutionState")]
    [InlineData("interface user repository", "IUserRepository")]
    [InlineData("interface token generator", "ITokenGenerator")]
    [InlineData("interface audio capture service", "IAudioCaptureService")]
    public void TypesAndInterfaces_TransformAccurately(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 3. CASING COMMANDS & HYPHENATION (10 cases)
    // =========================================================================
    [Theory]
    [InlineData("camel case api secret key", "apiSecretKey")]
    [InlineData("camel-case primary display resolution", "primaryDisplayResolution")]
    [InlineData("pascal case user profile manager", "UserProfileManager")]
    [InlineData("pascal-case sql connection factory", "SqlConnectionFactory")]
    [InlineData("snake case max retry attempts", "max_retry_attempts")]
    [InlineData("snake-case jwt authorization header", "jwt_authorization_header")]
    [InlineData("screaming snake case default timeout ms", "DEFAULT_TIMEOUT_MS")]
    [InlineData("constant case buffer size bytes", "BUFFER_SIZE_BYTES")]
    [InlineData("kebab case primary button component", "primary-button-component")]
    [InlineData("kebab-case user auth service", "user-auth-service")]
    public void CasingCommands_TransformAccurately(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 4. CODE OPERATORS & PUNCTUATION (10 cases)
    // =========================================================================
    [Theory]
    [InlineData("items dot map item fat arrow item dot id", "items.map item => item.id")]
    [InlineData("pointer thin arrow value", "pointer -> value")]
    [InlineData("if value double equals 0", "if value == 0")]
    [InlineData("if status not equals completed", "if status != completed")]
    [InlineData("const x equals 42", "const x = 42")]
    [InlineData("object dot property", "object.property")]
    [InlineData("my underscore value", "my_value")]
    [InlineData("wrap in backtick sample code backtick", "Wrap in `sample code`")]
    [InlineData("if count double equals 100", "if count == 100")]
    [InlineData("pointer thin arrow next", "pointer -> next")]
    public void CodePunctuation_TransformsAccurately(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Equal(expected, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 5. FILE PATHS & @FILE TAGGING (15 cases)
    // =========================================================================
    [Theory]
    [InlineData("at app dot ts", "@app.ts")]
    [InlineData("at user underscore profile dot cs", "@user_profile.cs")]
    [InlineData("at config dot json", "@config.json")]
    [InlineData("at src slash flow dot cs", "@src/flow.cs")]
    [InlineData("c colon backslash users backslash dev backslash project", @"C:\users\dev\project")]
    [InlineData("c colon backslash program files backslash dotnet", @"C:\Program Files\dotnet")]
    [InlineData(@"inspect C:\Users\dev\project\src\Program.cs now", @"C:\Users\dev\project\src\Program.cs")]
    [InlineData(@"run C:\Program Files\dotnet\dotnet.exe please", @"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData(@"check .\src\Program.cs carefully", @".\src\Program.cs")]
    [InlineData(@"open ..\tests\ProgramTests.cs here", @"..\tests\ProgramTests.cs")]
    [InlineData(@"look at src\Flow.Core\Context\DeveloperContext.cs", @"src\Flow.Core\Context\DeveloperContext.cs")]
    [InlineData(@"look at src/Flow.Core/Context/DeveloperContext.cs", @"src/Flow.Core/Context/DeveloperContext.cs")]
    [InlineData(@"binary at /usr/local/bin/python installed", @"/usr/local/bin/python")]
    [InlineData(@"entrypoint /home/dev/project/main.py found", @"/home/dev/project/main.py")]
    [InlineData(@"source at src/Flow.Core/Program.cs ready", @"src/Flow.Core/Program.cs")]
    public void FilePathsAndTags_RecognizedAccurately(string input, string expectedSub)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Contains(expectedSub, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 6. CLI COMMANDS & FLAGS (12 cases)
    // =========================================================================
    [Theory]
    [InlineData("git status", "git status")]
    [InlineData("git diff", "git diff")]
    [InlineData("git push --force", "git push --force")]
    [InlineData("dotnet test --no-incremental", "dotnet test --no-incremental")]
    [InlineData("npm run build", "npm run build")]
    [InlineData("docker compose up", "docker compose up")]
    [InlineData("kubectl get pods", "kubectl get pods")]
    [InlineData("shutdown /s /t 0", "shutdown /s /t 0")]
    [InlineData("Remove-Item -Recurse C:\\temp", "Remove-Item -Recurse C:\\temp")]
    [InlineData("del /s /q *", "del /s /q *")]
    [InlineData("dotnet restore --help", "dotnet restore --help")]
    [InlineData("powershell -Command", "powershell -Command")]
    public void CliCommandsAndFlags_PreservedWithoutExecution(string command, string expected)
    {
        string output = _pipeline.Format(command, TerminalOptions);
        Assert.Contains(expected, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 7. PROGRAMMING LANGUAGES & INFRASTRUCTURE (15 cases)
    // =========================================================================
    [Theory]
    [InlineData("written in C# and .NET 9", "C#")]
    [InlineData("compiled using C++ with clang", "C++")]
    [InlineData("backend built with ASP.NET Core", "ASP.NET Core")]
    [InlineData("desktop frontend uses WinUI 3", "WinUI 3")]
    [InlineData("typed using TypeScript compiler", "TypeScript")]
    [InlineData("client runtime is JavaScript", "JavaScript")]
    [InlineData("machine learning script in Python", "Python")]
    [InlineData("high performance code in Rust", "Rust")]
    [InlineData("microservice designed in Golang", "Golang")]
    [InlineData("packaged inside Docker container", "Docker")]
    [InlineData("orchestrated across Kubernetes cluster", "Kubernetes")]
    [InlineData("caching layer utilizes Redis", "Redis")]
    [InlineData("relational storage in PostgreSQL", "PostgreSQL")]
    [InlineData("lightweight database in SQLite", "SQLite")]
    [InlineData("audio pipeline uses WASAPI capture", "WASAPI")]
    public void LanguagesAndFrameworks_PreservedExactly(string input, string expectedEntity)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Contains(expectedEntity, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 8. ACRONYMS, IDENTIFIERS, URLS, EMAILS (15 cases)
    // =========================================================================
    [Theory]
    [InlineData("configure APIClientV2 for requests", "APIClientV2")]
    [InlineData("instantiate HTTP2Client instance", "HTTP2Client")]
    [InlineData("constant MAX_RETRY_COUNT defined", "MAX_RETRY_COUNT")]
    [InlineData("call getUserProfileAsync directly", "getUserProfileAsync")]
    [InlineData("import Flow.Core.Context.DeveloperContext", "Flow.Core.Context.DeveloperContext")]
    [InlineData("repo at https://github.com/Barathwaj2006/FLOW here", "https://github.com/Barathwaj2006/FLOW")]
    [InlineData("contact developer@flow.local today", "developer@flow.local")]
    [InlineData("encoded using UTF8 charset", "UTF8")]
    [InlineData("hardware accelerated with DirectML", "DirectML")]
    [InlineData("neural models converted to ONNX", "ONNX")]
    [InlineData("unique identifier is UUID token", "UUID")]
    [InlineData("session secured via JWT bearer", "JWT")]
    [InlineData("authenticate using OAuth 2 provider", "OAuth 2")]
    [InlineData("render Point2D coordinate", "Point2D")]
    [InlineData("low level Windows UI Automation support", "Windows UI Automation")]
    public void TechnicalTokensAndEntities_PreserveIntegrity(string input, string expectedToken)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Contains(expectedToken, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }

    // =========================================================================
    // 9. MULTILINGUAL INDIC + ENGLISH DEVELOPER SPEECH (8 cases)
    // =========================================================================
    [Theory]
    [InlineData("function user profile create பண்ணு", "userProfile")]
    [InlineData("camel case user profile", "userProfile")]
    [InlineData("class database repository உருவாக்கு", "DatabaseRepository")]
    [InlineData("function user profile बनाओ", "userProfile")]
    [InlineData("class database repository", "DatabaseRepository")]
    [InlineData("C# and .NET 9 use பண்ணு", "C#")]
    [InlineData("git push --force பண்ணாதே", "git push --force")]
    [InlineData("WASAPI audio capture சேர்", "WASAPI")]
    public void IndicCodeSwitchedSpeech_PreservesEnglishTechnicalEntities(string input, string expected)
    {
        string output = _pipeline.Format(input, CodeOptions);
        Assert.Contains(expected, output);
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
    }
}
