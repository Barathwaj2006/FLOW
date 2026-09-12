using System;

namespace Flow.Core.Developer;

/// <summary>
/// Strongly-typed developer operation types (WF-032, WF-035).
/// Represents structured developer intent derived deterministically from speech.
/// </summary>
public enum DeveloperOperationType
{
    FunctionDeclaration,
    ClassDeclaration,
    InterfaceDeclaration,
    StructDeclaration,
    RecordDeclaration,
    EnumDeclaration,
    CasingTransform,
    FileReference,
    PathReference,
    SpokenOperator,
    BacktickExpression
}

/// <summary>
/// Structured data describing a developer syntax or formatting operation.
/// The parser produces data; the application decides whether and how to format.
/// </summary>
public sealed record DeveloperOperation(
    DeveloperOperationType Type,
    string RawTarget,
    string FormattedOutput,
    int StartIndex,
    int Length,
    float Confidence,
    string? Language = null
);
