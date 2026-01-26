namespace JellyBox.Models;

internal sealed class Section
{
    public required string Name { get; set; }

    public required IReadOnlyList<Card>? Cards { get; set; }
}