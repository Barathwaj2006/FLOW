using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Core.Language;

/// <summary>
/// Authoritative catalog of spoken languages supported by FLOW and Whisper local models.
/// Maps ISO 639-1 language codes, native script names, and Whisper decoding parameters.
/// </summary>
public static class LanguageCatalog
{
    public static readonly LanguageInfo Auto = new(
        Code: LanguageCode.Auto,
        DisplayName: "Auto-Detect",
        NativeName: "Automatic",
        Script: "Universal",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo English = new(
        Code: LanguageCode.English,
        DisplayName: "English",
        NativeName: "English",
        Script: "Latin",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo Tamil = new(
        Code: LanguageCode.Tamil,
        DisplayName: "Tamil",
        NativeName: "தமிழ்",
        Script: "Tamil",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo Hindi = new(
        Code: LanguageCode.Hindi,
        DisplayName: "Hindi",
        NativeName: "हिन्दी",
        Script: "Devanagari",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo Spanish = new(
        Code: LanguageCode.Spanish,
        DisplayName: "Spanish",
        NativeName: "Español",
        Script: "Latin",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo French = new(
        Code: LanguageCode.French,
        DisplayName: "French",
        NativeName: "Français",
        Script: "Latin",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo German = new(
        Code: LanguageCode.German,
        DisplayName: "German",
        NativeName: "Deutsch",
        Script: "Latin",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo Japanese = new(
        Code: LanguageCode.Japanese,
        DisplayName: "Japanese",
        NativeName: "日本語",
        Script: "Kanji/Kana",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    public static readonly LanguageInfo Chinese = new(
        Code: LanguageCode.Chinese,
        DisplayName: "Chinese",
        NativeName: "中文",
        Script: "Hanzi",
        IsRtl: false,
        IsMultilingualSupported: true
    );

    private static readonly Dictionary<string, LanguageInfo> ByCode;
    private static readonly Dictionary<string, LanguageInfo> ByName;

    public static IReadOnlyList<LanguageInfo> SupportedLanguages { get; }

    static LanguageCatalog()
    {
        var languages = new List<LanguageInfo>
        {
            Auto,
            English,
            Tamil,
            Hindi,
            Spanish,
            French,
            German,
            Japanese,
            Chinese,
            new(new("it"), "Italian", "Italiano", "Latin"),
            new(new("pt"), "Portuguese", "Português", "Latin"),
            new(new("ru"), "Russian", "Русский", "Cyrillic"),
            new(new("ar"), "Arabic", "العربية", "Arabic", IsRtl: true),
            new(new("te"), "Telugu", "తెలుగు", "Telugu"),
            new(new("kn"), "Kannada", "ಕನ್ನಡ", "Kannada"),
            new(new("ml"), "Malayalam", "മലയാളം", "Malayalam"),
            new(new("mr"), "Marathi", "मराठी", "Devanagari"),
            new(new("bn"), "Bengali", "বাংলা", "Bengali"),
            new(new("gu"), "Gujarati", "ગુજરાતી", "Gujarati"),
            new(new("pa"), "Punjabi", "ਪੰਜਾਬੀ", "Gurmukhi"),
            new(new("ur"), "Urdu", "اردو", "Arabic", IsRtl: true),
            new(new("ko"), "Korean", "한국어", "Hangul"),
            new(new("nl"), "Dutch", "Nederlands", "Latin"),
            new(new("pl"), "Polish", "Polski", "Latin"),
            new(new("tr"), "Turkish", "Türkçe", "Latin"),
            new(new("sv"), "Swedish", "Svenska", "Latin"),
            new(new("id"), "Indonesian", "Bahasa Indonesia", "Latin")
        };

        SupportedLanguages = languages.AsReadOnly();
        ByCode = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        ByName = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in languages)
        {
            ByCode[lang.Code.Value] = lang;
            ByName[lang.DisplayName] = lang;
            ByName[lang.NativeName] = lang;
        }
    }

    public static bool TryGetLanguage(string? codeOrName, out LanguageInfo language)
    {
        language = English;
        if (string.IsNullOrWhiteSpace(codeOrName))
        {
            return false;
        }

        string trimmed = codeOrName.Trim();
        if (ByCode.TryGetValue(trimmed, out var match) || ByName.TryGetValue(trimmed, out match))
        {
            language = match;
            return true;
        }

        return false;
    }

    public static LanguageInfo GetLanguageOrDefault(string? codeOrName, LanguageInfo? defaultFallback = null)
    {
        if (TryGetLanguage(codeOrName, out var found))
        {
            return found;
        }
        return defaultFallback ?? English;
    }

    public static bool IsSupported(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        return ByCode.ContainsKey(code.Trim());
    }
}
