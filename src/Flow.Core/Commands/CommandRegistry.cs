using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Core.Context;

namespace Flow.Core.Commands;

/// <summary>
/// Immutable definition of a statically registered voice command.
/// Dynamic method execution from arbitrary speech is strictly prohibited.
/// </summary>
public sealed record CommandDefinition(
    string Id,
    string Name,
    IReadOnlyList<string> Aliases,
    CommandIntentType IntentType,
    CommandRisk Risk,
    CommandPermission RequiredPermissions,
    IReadOnlyList<ApplicationCategory> SupportedContexts,
    bool IsReversible,
    bool RequiresConfirmation
);

/// <summary>
/// Registry interface managing typed command definitions (Section 8).
/// </summary>
public interface ICommandRegistry
{
    IReadOnlyList<CommandDefinition> GetAllCommands();
    bool TryFindCommand(string phrase, out CommandDefinition? command);
    void RegisterCommand(CommandDefinition command);
}

/// <summary>
/// Default pre-populated command registry with built-in safe Windows commands and transforms.
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly List<CommandDefinition> _commands = new();
    private readonly Dictionary<string, CommandDefinition> _aliasIndex = new(StringComparer.OrdinalIgnoreCase);

    public CommandRegistry()
    {
        RegisterBuiltInCommands();
    }

    public IReadOnlyList<CommandDefinition> GetAllCommands() => _commands.AsReadOnly();

    public bool TryFindCommand(string phrase, out CommandDefinition? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(phrase)) return false;

        string normalized = phrase.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
        return _aliasIndex.TryGetValue(normalized, out command);
    }

    public void RegisterCommand(CommandDefinition command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        _commands.Add(command);
        foreach (var alias in command.Aliases)
        {
            _aliasIndex[alias.Trim().ToLowerInvariant()] = command;
        }
    }

    private void RegisterBuiltInCommands()
    {
        var allContexts = new[]
        {
            ApplicationCategory.GeneralProse,
            ApplicationCategory.Code,
            ApplicationCategory.Terminal,
            ApplicationCategory.Browser,
            ApplicationCategory.Document
        };

        // 1. In-Editor Actions
        RegisterCommand(new CommandDefinition(
            "cmd.copy",
            "Copy Selection",
            new[] { "copy", "copy that", "copy this", "copy selection" },
            CommandIntentType.Copy,
            CommandRisk.Safe,
            CommandPermission.ClipboardAccess,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "cmd.paste",
            "Paste Clipboard",
            new[] { "paste", "paste that", "insert clipboard" },
            CommandIntentType.Paste,
            CommandRisk.Safe,
            CommandPermission.ClipboardAccess | CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "cmd.undo",
            "Undo Last Action",
            new[] { "undo", "undo that", "revert", "ரத்து செய்", "पूर्ववत करो" },
            CommandIntentType.Undo,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "cmd.redo",
            "Redo Action",
            new[] { "redo", "redo that" },
            CommandIntentType.Redo,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "cmd.select_all",
            "Select All Text",
            new[] { "select all", "highlight all", "select everything" },
            CommandIntentType.SelectAll,
            CommandRisk.Safe,
            CommandPermission.TextReadSelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "cmd.deselect",
            "Deselect Text",
            new[] { "deselect", "clear selection", "unselect" },
            CommandIntentType.Deselect,
            CommandRisk.Safe,
            CommandPermission.TextReadSelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "cmd.delete_selection",
            "Delete Selection",
            new[] { "delete this", "delete selection", "remove selection", "delete that" },
            CommandIntentType.DeleteSelection,
            CommandRisk.ConfirmRequired,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: true
        ));

        // 2. Cancellation
        RegisterCommand(new CommandDefinition(
            "cmd.cancel",
            "Cancel Command",
            new[] { "cancel", "never mind", "stop", "abort" },
            CommandIntentType.CancelCommand,
            CommandRisk.Safe,
            CommandPermission.None,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        // 3. Selection Transforms
        RegisterCommand(new CommandDefinition(
            "transform.bullets",
            "Format as Bullets",
            new[] { "make bullet points", "bullet points", "bullets", "make bullets", "format as bullets", "turn into bullets", "convert to bullets" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.numbered_list",
            "Format as Numbered List",
            new[] { "make numbered list", "numbered list", "number this", "number list", "format as numbered list", "convert to numbers" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.uppercase",
            "Uppercase Text",
            new[] { "make uppercase", "uppercase", "all caps", "capitalize all", "to uppercase" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.lowercase",
            "Lowercase Text",
            new[] { "make lowercase", "lowercase", "all lowercase", "to lowercase" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.titlecase",
            "Title Case Text",
            new[] { "make title case", "title case", "capitalize words", "to title case" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.camelcase",
            "Camel Case Text",
            new[] { "make camel case", "to camel case", "camel case this" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.snakecase",
            "Snake Case Text",
            new[] { "make snake case", "to snake case", "snake case this" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.pascalcase",
            "Pascal Case Text",
            new[] { "make pascal case", "to pascal case", "pascal case this" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.kebabcase",
            "Kebab Case Text",
            new[] { "make kebab case", "to kebab case", "kebab case this" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.quotes",
            "Wrap in Quotes",
            new[] { "wrap in quotes", "put in quotes", "add quotes", "quotes", "surround with quotes" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.backticks",
            "Wrap in Backticks",
            new[] { "wrap in backticks", "add backticks", "put in backticks", "inline code" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.codeblock",
            "Wrap in Code Block",
            new[] { "make code block", "make a code block", "wrap in code block", "code block" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.trim",
            "Trim Whitespace",
            new[] { "trim whitespace", "clean up whitespace", "trim spaces", "remove extra spaces" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.concise",
            "Make Concise",
            new[] { "make concise", "make this concise", "shorter", "shorten this", "summarize" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.formal",
            "Make Formal",
            new[] { "make formal", "make this formal", "formalize", "expand contractions" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.fix_whitespace",
            "Fix Whitespace",
            new[] { "fix whitespace", "normalize whitespace", "clean up lines" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.fix_punctuation",
            "Fix Punctuation",
            new[] { "fix punctuation", "normalize punctuation", "clean punctuation" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.normalize_spacing",
            "Normalize Spacing",
            new[] { "normalize spacing", "fix spacing", "clean spacing" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));

        RegisterCommand(new CommandDefinition(
            "transform.normalize_quotes",
            "Normalize Quotes",
            new[] { "normalize quotes", "fix quotes", "standardize quotes" },
            CommandIntentType.TransformSelection,
            CommandRisk.Safe,
            CommandPermission.TextModifySelection,
            allContexts,
            IsReversible: true,
            RequiresConfirmation: false
        ));
    }
}
