using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;

namespace Flow.Host.Windows.Personalization;

/// <summary>
/// ViewModel for Personal Dictionary and Custom Corrections ("Teach FLOW your words").
/// </summary>
public sealed class DictionaryViewModel : INotifyPropertyChanged
{
    private readonly IPersonalDictionaryRepository _repository;
    private readonly PersonalDictionaryEngine? _engine;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private string? _statusMessage;
    private List<DictionaryEntry> _allEntries = new();

    public ObservableCollection<DictionaryEntry> DisplayEntries { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                FilterEntries();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsEmpty => DisplayEntries.Count == 0 && !IsLoading;

    public DictionaryViewModel(IPersonalDictionaryRepository repository, PersonalDictionaryEngine? engine = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _engine = engine;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var list = await _repository.GetAllAsync(ct);
            _allEntries = list.OrderByDescending(e => e.IsStarred).ThenBy(e => e.Term).ToList();
            FilterEntries();
            if (_engine != null)
            {
                await _engine.ReloadAsync(ct);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading dictionary: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public async Task<bool> AddOrUpdateEntryAsync(string term, string? replacement, bool isStarred = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return false;

        try
        {
            var existing = await _repository.GetByTermAsync(term.Trim(), ct);
            if (existing != null)
            {
                existing.Replacement = string.IsNullOrWhiteSpace(replacement) ? null : replacement.Trim();
                existing.IsStarred = isStarred;
                existing.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(existing, ct);
            }
            else
            {
                var newEntry = new DictionaryEntry
                {
                    Term = term.Trim(),
                    Replacement = string.IsNullOrWhiteSpace(replacement) ? null : replacement.Trim(),
                    IsStarred = isStarred,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _repository.AddAsync(newEntry, ct);
            }

            await RefreshAsync(ct);
            StatusMessage = $"Added \"{term.Trim()}\" to dictionary.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save word: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> ToggleStarAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var entry = await _repository.GetByIdAsync(id, ct);
            if (entry == null) return false;

            entry.IsStarred = !entry.IsStarred;
            entry.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entry, ct);
            await RefreshAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error toggling favorite: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteEntryAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await _repository.DeleteAsync(id, ct);
            await RefreshAsync(ct);
            StatusMessage = "Word removed from dictionary.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting word: {ex.Message}";
            return false;
        }
    }

    private void FilterEntries()
    {
        DisplayEntries.Clear();
        var query = _searchQuery.Trim();
        var items = string.IsNullOrEmpty(query)
            ? _allEntries
            : _allEntries.Where(e =>
                e.Term.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (e.Replacement != null && e.Replacement.Contains(query, StringComparison.OrdinalIgnoreCase)));

        foreach (var item in items)
        {
            DisplayEntries.Add(item);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}

/// <summary>
/// ViewModel for Voice Snippets ("Save text you use again and again").
/// </summary>
public sealed class SnippetsViewModel : INotifyPropertyChanged
{
    private readonly ISnippetRepository _repository;
    private readonly SnippetExpansionEngine? _engine;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private string? _statusMessage;
    private List<SnippetEntry> _allSnippets = new();

    public ObservableCollection<SnippetEntry> DisplaySnippets { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                FilterSnippets();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsEmpty => DisplaySnippets.Count == 0 && !IsLoading;

    public SnippetsViewModel(ISnippetRepository repository, SnippetExpansionEngine? engine = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _engine = engine;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var list = await _repository.GetAllAsync(ct);
            _allSnippets = list.OrderBy(s => s.TriggerPhrase).ToList();
            FilterSnippets();
            if (_engine != null)
            {
                await _engine.ReloadAsync(ct);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading snippets: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public async Task<bool> AddOrUpdateSnippetAsync(string trigger, string expansion, string? description = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(expansion)) return false;

        try
        {
            var existing = await _repository.GetByTriggerAsync(trigger.Trim(), ct);
            if (existing != null)
            {
                existing.ExpansionText = expansion.Trim();
                existing.Description = description?.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(existing, ct);
            }
            else
            {
                var newSnippet = new SnippetEntry
                {
                    TriggerPhrase = trigger.Trim(),
                    ExpansionText = expansion.Trim(),
                    Description = description?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _repository.AddAsync(newSnippet, ct);
            }

            await RefreshAsync(ct);
            StatusMessage = $"Saved snippet for \"{trigger.Trim()}\".";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save snippet: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteSnippetAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await _repository.DeleteAsync(id, ct);
            await RefreshAsync(ct);
            StatusMessage = "Snippet deleted.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting snippet: {ex.Message}";
            return false;
        }
    }

    private void FilterSnippets()
    {
        DisplaySnippets.Clear();
        var query = _searchQuery.Trim();
        var items = string.IsNullOrEmpty(query)
            ? _allSnippets
            : _allSnippets.Where(s =>
                s.TriggerPhrase.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.ExpansionText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (s.Description != null && s.Description.Contains(query, StringComparison.OrdinalIgnoreCase)));

        foreach (var item in items)
        {
            DisplaySnippets.Add(item);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}

/// <summary>
/// ViewModel for Writing Styles ("Make FLOW sound like you").
/// </summary>
public sealed class StylesViewModel : INotifyPropertyChanged
{
    private readonly IStyleRepository _repository;
    private readonly StyleFormattingEngine? _engine;
    private StyleProfile? _activeProfile;
    private bool _isLoading;
    private string? _statusMessage;

    public ObservableCollection<StyleProfile> Profiles { get; } = new();

    public StyleProfile? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (_activeProfile != value)
            {
                _activeProfile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveProfileName));
            }
        }
    }

    public string ActiveProfileName => _activeProfile?.Name ?? "Natural / Balanced";

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public StylesViewModel(IStyleRepository repository, StyleFormattingEngine? engine = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _engine = engine;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var list = await _repository.GetAllProfilesAsync(ct);
            Profiles.Clear();

            // Guarantee standard styles exist
            if (list.Count == 0)
            {
                var standardProfiles = new[]
                {
                    new StyleProfile { Name = "Natural / Balanced", Description = "Clear, natural formatting with standard punctuation and capitalization.", FormalityLevel = FormalityLevel.Balanced, ContractionPolicy = ContractionPolicy.Preserve, IsEnabled = true },
                    new StyleProfile { Name = "Formal / Professional", Description = "Expands contractions ('do not' instead of 'don't') with polished sentence structure.", FormalityLevel = FormalityLevel.Formal, ContractionPolicy = ContractionPolicy.Expand, IsEnabled = false },
                    new StyleProfile { Name = "Casual / Conversational", Description = "Preserves natural spoken contractions and relaxed cadence for messaging.", FormalityLevel = FormalityLevel.Casual, ContractionPolicy = ContractionPolicy.Contract, IsEnabled = false },
                    new StyleProfile { Name = "Concise / Bulleted", Description = "Presents brief, punchy expressions optimized for rapid capture and notes.", FormalityLevel = FormalityLevel.Balanced, ContractionPolicy = ContractionPolicy.Preserve, UseBulletPoints = true, IsEnabled = false }
                };

                foreach (var sp in standardProfiles)
                {
                    await _repository.SaveProfileAsync(sp, ct);
                    Profiles.Add(sp);
                }
            }
            else
            {
                foreach (var p in list)
                {
                    Profiles.Add(p);
                }
            }

            ActiveProfile = Profiles.FirstOrDefault(p => p.IsEnabled) ?? Profiles.FirstOrDefault();
            if (_engine != null)
            {
                await _engine.ReloadAsync(ct);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading styles: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectProfileAsync(string profileId, CancellationToken ct = default)
    {
        try
        {
            var target = Profiles.FirstOrDefault(p => p.Id == profileId);
            if (target == null) return;

            foreach (var p in Profiles)
            {
                p.IsEnabled = (p.Id == profileId);
                await _repository.SaveProfileAsync(p, ct);
            }

            ActiveProfile = target;
            if (_engine != null)
            {
                await _engine.ReloadAsync(ct);
            }
            StatusMessage = $"Active style set to \"{target.Name}\".";
            OnPropertyChanged(nameof(Profiles));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting style: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
