using System;
using System.Collections.Generic;
using System.IO;
using Flow.Core.Language;

namespace Flow.Core.Developer;

/// <summary>
/// Language-specific coding conventions profile (WF-035).
/// Governs deterministic casing and syntax conventions according to language idioms.
/// </summary>
public sealed record DeveloperLanguageProfile(
    string LanguageId,
    string DisplayName,
    IReadOnlyList<string> FileExtensions,
    IdentifierCasingStyle FunctionCasing,
    IdentifierCasingStyle ClassCasing,
    IdentifierCasingStyle InterfaceCasing,
    string? InterfacePrefix,
    IdentifierCasingStyle ConstantCasing,
    IdentifierCasingStyle LocalVariableCasing
);

/// <summary>
/// Catalog of deterministic language profiles for major programming languages.
/// </summary>
public static class LanguageProfileCatalog
{
    public static readonly DeveloperLanguageProfile CSharp = new(
        LanguageId: "csharp",
        DisplayName: "C#",
        FileExtensions: new[] { ".cs", ".csx" },
        FunctionCasing: IdentifierCasingStyle.PascalCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: "I",
        ConstantCasing: IdentifierCasingStyle.PascalCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile Python = new(
        LanguageId: "python",
        DisplayName: "Python",
        FileExtensions: new[] { ".py", ".pyw", ".ipynb" },
        FunctionCasing: IdentifierCasingStyle.SnakeCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.SnakeCase
    );

    public static readonly DeveloperLanguageProfile TypeScript = new(
        LanguageId: "typescript",
        DisplayName: "TypeScript",
        FileExtensions: new[] { ".ts", ".tsx" },
        FunctionCasing: IdentifierCasingStyle.CamelCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile JavaScript = new(
        LanguageId: "javascript",
        DisplayName: "JavaScript",
        FileExtensions: new[] { ".js", ".jsx", ".mjs", ".cjs" },
        FunctionCasing: IdentifierCasingStyle.CamelCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile Rust = new(
        LanguageId: "rust",
        DisplayName: "Rust",
        FileExtensions: new[] { ".rs" },
        FunctionCasing: IdentifierCasingStyle.SnakeCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.SnakeCase
    );

    public static readonly DeveloperLanguageProfile Go = new(
        LanguageId: "go",
        DisplayName: "Go",
        FileExtensions: new[] { ".go" },
        FunctionCasing: IdentifierCasingStyle.PascalCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.PascalCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile Cpp = new(
        LanguageId: "cpp",
        DisplayName: "C++",
        FileExtensions: new[] { ".cpp", ".cxx", ".cc", ".h", ".hpp" },
        FunctionCasing: IdentifierCasingStyle.CamelCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: "I",
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile Java = new(
        LanguageId: "java",
        DisplayName: "Java",
        FileExtensions: new[] { ".java" },
        FunctionCasing: IdentifierCasingStyle.CamelCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile Kotlin = new(
        LanguageId: "kotlin",
        DisplayName: "Kotlin",
        FileExtensions: new[] { ".kt", ".kts" },
        FunctionCasing: IdentifierCasingStyle.CamelCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile Swift = new(
        LanguageId: "swift",
        DisplayName: "Swift",
        FileExtensions: new[] { ".swift" },
        FunctionCasing: IdentifierCasingStyle.CamelCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: null,
        ConstantCasing: IdentifierCasingStyle.CamelCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    public static readonly DeveloperLanguageProfile Generic = new(
        LanguageId: "generic",
        DisplayName: "Generic",
        FileExtensions: Array.Empty<string>(),
        FunctionCasing: IdentifierCasingStyle.CamelCase,
        ClassCasing: IdentifierCasingStyle.PascalCase,
        InterfaceCasing: IdentifierCasingStyle.PascalCase,
        InterfacePrefix: "I",
        ConstantCasing: IdentifierCasingStyle.ScreamingSnakeCase,
        LocalVariableCasing: IdentifierCasingStyle.CamelCase
    );

    private static readonly Dictionary<string, DeveloperLanguageProfile> ById = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = CSharp,
        ["cs"] = CSharp,
        ["c#"] = CSharp,
        ["python"] = Python,
        ["py"] = Python,
        ["typescript"] = TypeScript,
        ["ts"] = TypeScript,
        ["javascript"] = JavaScript,
        ["js"] = JavaScript,
        ["rust"] = Rust,
        ["rs"] = Rust,
        ["go"] = Go,
        ["golang"] = Go,
        ["cpp"] = Cpp,
        ["c++"] = Cpp,
        ["java"] = Java,
        ["kotlin"] = Kotlin,
        ["kt"] = Kotlin,
        ["swift"] = Swift
    };

    public static DeveloperLanguageProfile Resolve(string? fileContext, string? languageHint = null)
    {
        if (!string.IsNullOrWhiteSpace(languageHint) && ById.TryGetValue(languageHint.Trim(), out var hintProfile))
        {
            return hintProfile;
        }

        if (!string.IsNullOrWhiteSpace(fileContext))
        {
            string ext = Path.GetExtension(fileContext).ToLowerInvariant();
            foreach (var profile in ById.Values)
            {
                foreach (var pExt in profile.FileExtensions)
                {
                    if (string.Equals(ext, pExt, StringComparison.OrdinalIgnoreCase))
                    {
                        return profile;
                    }
                }
            }
        }

        return Generic;
    }
}
