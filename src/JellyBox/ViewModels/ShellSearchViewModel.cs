using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Models;
using JellyBox.Services;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class ShellSearchViewModel : ObservableObject
#pragma warning restore CA1812
{
    private const int MinimumQueryLength = 2;
    private const int SuggestionLimit = 8;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(350);

    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;
    private int _searchVersion;
    private bool _suppressSuggestions;

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    public ObservableCollection<SearchSuggestion> Suggestions { get; } = [];

    public ShellSearchViewModel(
        JellyfinApiClient jellyfinApiClient,
        NavigationManager navigationManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _navigationManager = navigationManager;
    }

    partial void OnQueryChanged(string value)
    {
        if (!_suppressSuggestions)
        {
            _ = UpdateSuggestionsAsync(value);
        }
    }

    public void SubmitQuery(string? query)
    {
        string trimmed = (query ?? Query).Trim();
        if (trimmed.Length < MinimumQueryLength)
        {
            return;
        }

        Query = trimmed;
        _navigationManager.NavigateToSearch(trimmed);
    }

    public void ClearQuery()
    {
        Interlocked.Increment(ref _searchVersion);
        Query = string.Empty;
        ReplaceSuggestions([]);
    }

    public void SelectSuggestion(SearchSuggestion suggestion)
    {
        _suppressSuggestions = true;
        try
        {
            Interlocked.Increment(ref _searchVersion);
            Query = string.Empty;
            ReplaceSuggestions([]);
            _navigationManager.NavigateToItem(suggestion.ItemId);
        }
        finally
        {
            _suppressSuggestions = false;
        }
    }

    private async Task UpdateSuggestionsAsync(string query)
    {
        int version = Interlocked.Increment(ref _searchVersion);

        string trimmed = query.Trim();
        if (trimmed.Length < MinimumQueryLength)
        {
            ReplaceSuggestions([]);
            return;
        }

        try
        {
            await Task.Delay(DebounceDelay);

            if (version != _searchVersion)
            {
                return;
            }

            SearchHintResult? result = await _jellyfinApiClient.Search.Hints.GetAsync(
                parameters =>
                {
                    parameters.QueryParameters.SearchTerm = trimmed;
                    parameters.QueryParameters.IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series];
                    parameters.QueryParameters.IncludeMedia = true;
                    parameters.QueryParameters.Limit = SuggestionLimit;
                });

            if (version != _searchVersion)
            {
                return;
            }

            if (result?.SearchHints is null)
            {
                ReplaceSuggestions([]);
                return;
            }

            List<SearchSuggestion> suggestions = new(result.SearchHints.Count);
            foreach (SearchHint hint in result.SearchHints)
            {
                SearchSuggestion? suggestion = CreateSuggestion(hint);
                if (suggestion is not null)
                {
                    suggestions.Add(suggestion);
                }
            }

            ReplaceSuggestions(suggestions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ShellSearchViewModel.UpdateSuggestionsAsync: {ex}");
            ReplaceSuggestions([]);
        }
    }

    private void ReplaceSuggestions(IEnumerable<SearchSuggestion> suggestions)
    {
        Suggestions.Clear();
        foreach (SearchSuggestion suggestion in suggestions)
        {
            Suggestions.Add(suggestion);
        }
    }

    private static SearchSuggestion? CreateSuggestion(SearchHint hint)
    {
        if (hint.Type is not (SearchHint_Type.Movie or SearchHint_Type.Series))
        {
            return null;
        }

        if (!hint.Id.HasValue || string.IsNullOrWhiteSpace(hint.Name))
        {
            return null;
        }

        string typeLabel = hint.Type == SearchHint_Type.Movie ? "Movie" : "TV Show";
        string? secondaryText = hint.ProductionYear.HasValue
            ? $"{typeLabel} · {hint.ProductionYear.Value}"
            : typeLabel;

        return new SearchSuggestion(hint.Name, secondaryText, hint.Id.Value);
    }
}