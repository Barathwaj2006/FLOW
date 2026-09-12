using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Flow.Core.Storage;

/// <summary>
/// Manages the local SQLite database for FLOW personalization:
/// Personal Dictionary, Voice Snippets, and Style Profiles.
/// Operates 100% offline with zero cloud transmission.
/// </summary>
public sealed class SqlitePersonalizationDatabase : IDisposable
{
    private readonly string _databasePath;
    private readonly bool _isMemory;
    private SqliteConnection? _memoryKeepAliveConnection;
    private bool _disposed;

    public string DatabasePath => _databasePath;

    public SqlitePersonalizationDatabase(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string flowDir = Path.Combine(localAppData, "FLOW");
            Directory.CreateDirectory(flowDir);
            _databasePath = Path.Combine(flowDir, "flow_personalization.db");
            _isMemory = false;
        }
        else if (dbPath.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            _databasePath = $"mem_{Guid.NewGuid():N}";
            _isMemory = true;
        }
        else
        {
            _databasePath = dbPath;
            _isMemory = false;
            string? dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        if (_isMemory)
        {
            // Keep-alive connection so in-memory database survives across calls
            _memoryKeepAliveConnection = new SqliteConnection($"Data Source={_databasePath};Mode=Memory;Cache=Shared");
            _memoryKeepAliveConnection.Open();
        }

        InitializeSchema();
    }

    /// <summary>
    /// Creates and opens a new SQLite connection with foreign keys and WAL enabled.
    /// </summary>
    public SqliteConnection CreateConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string connStr = _isMemory
            ? $"Data Source={_databasePath};Mode=Memory;Cache=Shared"
            : $"Data Source={_databasePath};Mode=ReadWriteCreate";

        var conn = new SqliteConnection(connStr);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();

            if (!_isMemory)
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL;";
                cmd.ExecuteNonQuery();
            }
        }

        return conn;
    }

    /// <summary>
    /// Initializes tables, indices, and baseline default profiles.
    /// </summary>
    public void InitializeSchema()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS DictionaryEntries (
                Id TEXT PRIMARY KEY,
                Term TEXT NOT NULL,
                Replacement TEXT,
                IsStarred INTEGER NOT NULL DEFAULT 0,
                Category TEXT,
                CaseSensitive INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_dict_term ON DictionaryEntries(Term COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS Snippets (
                Id TEXT PRIMARY KEY,
                TriggerPhrase TEXT NOT NULL,
                ExpansionText TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                Category TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_snippet_trigger ON Snippets(TriggerPhrase COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS StyleProfiles (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE,
                Description TEXT,
                ContractionPolicy INTEGER NOT NULL DEFAULT 0,
                FormalityLevel INTEGER NOT NULL DEFAULT 1,
                UseBulletPoints INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AppStyleMappings (
                ProcessName TEXT PRIMARY KEY,
                StyleProfileId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (StyleProfileId) REFERENCES StyleProfiles(Id) ON DELETE CASCADE
            );
        ";
        cmd.ExecuteNonQuery();

        SeedDefaultStyleProfiles(conn);
    }

    private static void SeedDefaultStyleProfiles(SqliteConnection conn)
    {
        string now = DateTime.UtcNow.ToString("O");
        var defaults = new (string Id, string Name, string Description, int Contractions, int Formality, int Bullets)[]
        {
            ("style_default", "Default", "Standard neutral voice formatting", 0, 1, 0),
            ("style_personal", "Personal", "Casual and expressive tone with contractions", 2, 0, 0),
            ("style_work", "Work", "Polished and balanced business communication", 0, 2, 0),
            ("style_email", "Email", "Professional email communication with expanded contractions", 1, 2, 0),
            ("style_technical", "Technical", "Formal precision preserving code identifiers and technical terms", 0, 2, 0),
            ("style_casual", "Casual", "Informal conversational tone", 2, 0, 0)
        };

        foreach (var (id, name, desc, contractions, formality, bullets) in defaults)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO StyleProfiles (Id, Name, Description, ContractionPolicy, FormalityLevel, UseBulletPoints, CreatedAt, UpdatedAt)
                VALUES (@id, @name, @desc, @contractions, @formality, @bullets, @now, @now);
            ";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@desc", desc);
            cmd.Parameters.AddWithValue("@contractions", contractions);
            cmd.Parameters.AddWithValue("@formality", formality);
            cmd.Parameters.AddWithValue("@bullets", bullets);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_memoryKeepAliveConnection != null)
        {
            _memoryKeepAliveConnection.Close();
            _memoryKeepAliveConnection.Dispose();
            _memoryKeepAliveConnection = null;
        }
    }
}
