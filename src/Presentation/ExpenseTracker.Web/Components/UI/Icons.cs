namespace ExpenseTracker.Web.Components.UI;

/// <summary>
/// Every icon of the UI, by Bootstrap Icons name. Pages use these constants instead of string literals,
/// so a typo does not compile and switching icon provider means editing this file and AppIcon only.
/// </summary>
public static class Icons
{
    // Brand and navigation
    public const string Logo = "wallet2";
    public const string Home = "house";
    public const string HomeActive = "house-fill";
    public const string Incomes = "cash-coin";
    public const string Expenses = "receipt";
    public const string Debts = "bank";
    public const string Budget = "pie-chart";
    public const string Goals = "bullseye";
    public const string Forecast = "graph-up-arrow";
    public const string Settings = "gear";
    public const string User = "person-circle";
    public const string Logout = "box-arrow-right";
    public const string CollapseSidebar = "layout-sidebar";
    public const string BudgetActive = "pie-chart-fill";
    public const string Plans = "calendar-range";
    public const string History = "clock-history";
    public const string Projection = "graph-down-arrow";
    public const string SettingsActive = "gear-fill";
    public const string Login = "box-arrow-in-right";
    public const string Register = "person-plus";
    public const string Account = "person-gear";

    // Actions
    public const string Calendar = "calendar3";
    public const string CalendarCheck = "calendar-check";
    public const string Previous = "chevron-left";
    public const string Next = "chevron-right";
    public const string ChevronDown = "chevron-down";
    public const string Add = "plus-lg";
    public const string More = "three-dots";
    public const string Edit = "pencil";
    public const string Duplicate = "copy";
    public const string Delete = "trash3";
    public const string Close = "x-lg";
    public const string Check = "check-lg";
    public const string Search = "search";
    public const string Filter = "funnel";
    public const string Recurring = "arrow-repeat";

    // Status
    public const string OnTrack = "check-circle";
    public const string AtRisk = "exclamation-triangle";
    public const string Exceeded = "x-octagon";
    public const string Paid = "check2";
    public const string Pending = "clock";
    public const string Deficit = "exclamation-circle";
    public const string TrendUp = "arrow-up-short";
    public const string TrendDown = "arrow-down-short";

    // Goals
    public const string GoalOnTrack = "graph-up";
    public const string GoalBehind = "hourglass-split";
    public const string GoalDone = "trophy";

    // Feedback
    public const string Success = "check-circle";
    public const string Error = "x-circle";
    public const string Info = "info-circle";
    public const string Empty = "inbox";
    public const string EmptyGoals = "piggy-bank";
    public const string EmptyIncomes = "cash-stack";

    // Theme
    public const string ThemeLight = "sun";
    public const string ThemeDark = "moon";
    public const string ThemeSystem = "circle-half";

    /// <summary>Fallback for a category or goal that has no icon chosen.</summary>
    public const string DefaultCategory = "tag";

    /// <summary>Curated list the user picks from for categories and goals (never emojis or free text).</summary>
    public static readonly string[] Picker =
    {
        "basket", "bag", "lightning-charge", "bus-front", "car-front", "fuel-pump",
        "heart-pulse", "capsule", "film", "collection-play", "music-note-beamed",
        "shield-check", "piggy-bank", "tag", "house", "house-gear", "phone",
        "wifi", "droplet", "cup-hot", "egg-fried", "scissors", "airplane",
        "suitcase", "mortarboard", "book", "laptop", "controller", "gift",
        "balloon", "tree", "bicycle", "heart", "person-arms-up", "briefcase",
        "tools", "wrench", "cart", "credit-card", "cash-stack"
    };
}
