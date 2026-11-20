namespace ExpenseTracker.Web.Components.UI;

public enum OverlayKind
{
    /// <summary>Centered dialog.</summary>
    Modal,

    /// <summary>Panel that slides in from the right.</summary>
    Side
}

/// <summary>Maximum width of a modal: 440, 520, 720 and 1000 px.</summary>
public enum OverlayWidth
{
    Sm,
    Md,
    Lg,
    Xl
}
