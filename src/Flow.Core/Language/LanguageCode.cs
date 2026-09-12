using System;

namespace Flow.Core.Language;

/// <summary>
/// Strongly-typed value object representing an ISO 639-1 language code or the "auto" detection sentinel.
/// </summary>
public readonly struct LanguageCode : IEquatable<LanguageCode>
{
    private readonly string _code;

    public string Value => _code ?? "en";

    public bool IsAutoDetect => string.Equals(Value, "auto", StringComparison.OrdinalIgnoreCase);

    public LanguageCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            _code = "en";
            return;
        }

        string trimmed = code.Trim().ToLowerInvariant();
        _code = trimmed;
    }

    public static LanguageCode Auto => new("auto");
    public static LanguageCode English => new("en");
    public static LanguageCode Tamil => new("ta");
    public static LanguageCode Hindi => new("hi");
    public static LanguageCode Spanish => new("es");
    public static LanguageCode French => new("fr");
    public static LanguageCode German => new("de");
    public static LanguageCode Japanese => new("ja");
    public static LanguageCode Chinese => new("zh");

    public override string ToString() => Value;

    public bool Equals(LanguageCode other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) =>
        obj is LanguageCode other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool operator ==(LanguageCode left, LanguageCode right) => left.Equals(right);
    public static bool operator !=(LanguageCode left, LanguageCode right) => !left.Equals(right);

    public static implicit operator string(LanguageCode code) => code.Value;
    public static implicit operator LanguageCode(string code) => new(code);
}
