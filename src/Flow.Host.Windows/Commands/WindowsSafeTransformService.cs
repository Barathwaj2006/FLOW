using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Backtrack;
using Flow.Core.Commands;
using Flow.Core.TextInsertion;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Commands;

/// <summary>
/// Service executing selection-aware safe transforms on Windows (WF-037A).
/// Guarantees zero Enter simulation, selection bounds checking, and undo tracking.
/// </summary>
public sealed class WindowsSafeTransformService
{
    private readonly ITextTransformProvider _transformProvider;
    private readonly ITextInsertionService _insertionService;
    private readonly InsertionHistoryTracker? _historyTracker;
    private readonly ILogger<WindowsSafeTransformService>? _logger;

    public WindowsSafeTransformService(
        ITextTransformProvider transformProvider,
        ITextInsertionService insertionService,
        InsertionHistoryTracker? historyTracker = null,
        ILogger<WindowsSafeTransformService>? logger = null)
    {
        _transformProvider = transformProvider ?? throw new ArgumentNullException(nameof(transformProvider));
        _insertionService = insertionService ?? throw new ArgumentNullException(nameof(insertionService));
        _historyTracker = historyTracker;
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteTransformAsync(
        string selectedText,
        TransformType transform,
        CommandExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return CommandResult.CreateRejected("transform", CommandIntentType.TransformSelection, "No text selected to transform.");
        }

        // 1. Size bounds check
        var risk = TransformSafetyValidator.EvaluateTransformRisk(transform, selectedText);
        if (risk == CommandRisk.Blocked)
        {
            return CommandResult.CreateBlocked("transform", CommandIntentType.TransformSelection, "Selection exceeds maximum safe size bounds (>100k characters).");
        }

        // 2. Perform deterministic local transformation
        string transformed = _transformProvider.Transform(selectedText, transform);

        // 3. Inviolable Zero-Enter Invariant Check
        if (transformed.Contains('\r') || transformed.Contains('\n'))
        {
            transformed = transformed.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        // 4. Insert transformed text (replacing selection)
        var insertionResult = await _insertionService.InsertTextAsync(transformed, cancellationToken);
        if (!insertionResult.Success)
        {
            return new CommandResult(
                CommandResultStatus.Failed,
                "transform",
                CommandIntentType.TransformSelection,
                insertionResult.ErrorMessage ?? "Insertion failed",
                insertionResult.Latency,
                DateTimeOffset.UtcNow,
                insertionResult.TargetApplicationName
            );
        }

        // 5. Track in undo / backtrack history
        var record = new InsertionRecord(
            Guid.NewGuid(),
            transformed,
            insertionResult.InsertedLength > 0 ? insertionResult.InsertedLength : transformed.Length,
            DateTimeOffset.UtcNow,
            insertionResult.TargetHwnd,
            insertionResult.TargetApplicationName ?? context.TargetApplication,
            insertionResult.TargetProcessId,
            insertionResult.StrategyUsed
        );
        _historyTracker?.RecordInsertion(record);

        _logger?.LogInformation("Safe transform '{Transform}' applied successfully to {App}", transform, context.TargetApplication);
        return CommandResult.CreateSuccess("transform", CommandIntentType.TransformSelection, $"Transformed selection to {transform}", insertionResult.Latency);
    }
}
