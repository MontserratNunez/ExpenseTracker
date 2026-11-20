using Microsoft.AspNetCore.Components;

namespace ExpenseTracker.Web.Components.UI;

/// <summary>One option of a <see cref="DropdownMenu"/>.</summary>
public class DropdownMenuItem
{
    /// <param name="onSelect">
    /// Create it in the page with <c>EventCallback.Factory.Create(this, ...)</c>: an EventCallback tells its owner to
    /// re-render after it runs, which a plain delegate would not (the menu, not the page, would re-render).
    /// </param>
    public DropdownMenuItem(string label, string? icon = null, EventCallback onSelect = default, bool danger = false, bool disabled = false)
    {
        Label = label;
        Icon = icon;
        OnSelect = onSelect;
        Danger = danger;
        Disabled = disabled;
    }

    public string Label { get; }

    /// <summary>Bootstrap Icons name (use the constants in <see cref="Icons"/>).</summary>
    public string? Icon { get; }

    public EventCallback OnSelect { get; }

    /// <summary>A destructive option (delete): shown in red.</summary>
    public bool Danger { get; }

    public bool Disabled { get; }
}
