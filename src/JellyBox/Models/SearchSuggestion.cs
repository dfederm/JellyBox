namespace JellyBox.Models;

internal sealed record SearchSuggestion(string DisplayText, string? SecondaryText, Guid ItemId);