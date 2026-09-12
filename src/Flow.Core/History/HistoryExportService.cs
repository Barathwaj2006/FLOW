using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.History;

/// <summary>
/// Exports dictation history to local formats (JSON, CSV, Plain Text) with strict security controls (WF-041).
/// INVIOLABLE: Zero network transmission. Overwrite protection. Path traversal defense.
/// </summary>
public sealed class HistoryExportService : IHistoryExportService
{
    private readonly IHistoryRepository _repository;

    public HistoryExportService(IHistoryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<string> ExportAsync(
        string destinationFilePath,
        HistoryExportFormat format,
        HistoryFilter? filter = null,
        bool overwrite = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            throw new ArgumentException("Destination file path cannot be empty.", nameof(destinationFilePath));
        }

        // Security check: Path traversal, UNC, and device paths
        ValidateSafePath(destinationFilePath);

        if (File.Exists(destinationFilePath) && !overwrite)
        {
            throw new IOException($"Destination file '{destinationFilePath}' already exists and overwrite is disabled.");
        }

        string? dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var entries = await _repository.GetAllForExportAsync(filter, ct);

        string content = format switch
        {
            HistoryExportFormat.Json => ExportAsJson(entries),
            HistoryExportFormat.Csv => ExportAsCsv(entries),
            HistoryExportFormat.PlainText => ExportAsPlainText(entries),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.")
        };

        await File.WriteAllTextAsync(destinationFilePath, content, Encoding.UTF8, ct);
        return destinationFilePath;
    }

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static void ValidateSafePath(string path)
    {
        if (path.StartsWith(@"\\") && !path.StartsWith(@"\\localhost\", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("UNC network paths are rejected for security.", nameof(path));
        }

        if (path.StartsWith(@"\\.\") || path.StartsWith(@"\\?\"))
        {
            throw new ArgumentException("Device namespaces are rejected for security.", nameof(path));
        }

        if (path.Contains(".."))
        {
            throw new ArgumentException("Path traversal tokens are rejected for security.", nameof(path));
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        if (ReservedDeviceNames.Contains(fileName))
        {
            throw new ArgumentException($"Windows reserved device names ({fileName}) are rejected for security.", nameof(path));
        }

        string full = Path.GetFullPath(path);
        if (full.Contains(".."))
        {
            throw new ArgumentException("Path traversal tokens are rejected for security.", nameof(path));
        }
    }

    private static string ExportAsJson(IReadOnlyList<DictationEntry> entries)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(entries, options);
    }

    private static string ExportAsCsv(IReadOnlyList<DictationEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,SessionId,CreatedAt,DurationMs,WordCount,CharacterCount,Language,Application,Category,Mode,State,IsFavorite,Wpm,Text");

        foreach (var e in entries)
        {
            sb.Append(EscapeCsv(e.Id)).Append(',');
            sb.Append(EscapeCsv(e.SessionId.ToString())).Append(',');
            sb.Append(EscapeCsv(e.CreatedAt.ToString("O"))).Append(',');
            sb.Append(e.DurationMs).Append(',');
            sb.Append(e.WordCount).Append(',');
            sb.Append(e.CharacterCount).Append(',');
            sb.Append(EscapeCsv(e.Language)).Append(',');
            sb.Append(EscapeCsv(e.Application)).Append(',');
            sb.Append(EscapeCsv(e.ApplicationCategory)).Append(',');
            sb.Append(EscapeCsv(e.Mode)).Append(',');
            sb.Append(EscapeCsv(e.State.ToString())).Append(',');
            sb.Append(e.IsFavorite ? "1" : "0").Append(',');
            sb.Append(e.Wpm).Append(',');
            sb.AppendLine(EscapeCsv(e.Text ?? ""));
        }

        return sb.ToString();
    }

    private static string ExportAsPlainText(IReadOnlyList<DictationEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# FLOW Dictation History Export");
        sb.AppendLine($"# Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"# Total Entries: {entries.Count}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        foreach (var e in entries)
        {
            sb.AppendLine($"[{e.CreatedAt:yyyy-MM-dd HH:mm:ss}] | App: {e.Application} | Lang: {e.Language} | WPM: {e.Wpm} | Mode: {e.Mode}");
            if (!string.IsNullOrWhiteSpace(e.Text))
            {
                sb.AppendLine(e.Text);
            }
            sb.AppendLine(new string('-', 40));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        string escaped = value.Replace("\"", "\"\"");
        return "\"" + escaped + "\"";
    }
}
