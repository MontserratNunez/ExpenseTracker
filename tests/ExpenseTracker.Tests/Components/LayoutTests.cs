using Bunit;
using Bunit.TestDoubles;
using ExpenseTracker.Web.Components.Layout;
using ExpenseTracker.Web.Components.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Tests.Components;

/// <summary>The sidebar, the phone navigation and the list of destinations they share.</summary>
public class LayoutTests : UiTestContext
{
    private readonly BunitJSModuleInterop _module;

    public LayoutTests()
    {
        _module = JSInterop.SetupModule("./js/ui.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    // Services first, then the renderer info (setting it starts the service provider). Interactive by default.
    private void SignIn(string name = "ana@example.com")
    {
        AddAuthorization().SetAuthorized(name);
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private void SignedOut()
    {
        AddAuthorization();
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private void NavigateTo(string relative) => Services.GetRequiredService<NavigationManager>().NavigateTo(relative);

    // ================= NavItems =================

    [Theory]
    [InlineData("", "", true)]
    [InlineData("", "gastos", false)]
    [InlineData("gastos", "gastos", true)]
    [InlineData("gastos", "gastos/2026/11", true)]
    [InlineData("gastos", "gastos-viejos", false)]
    [InlineData("deudas", "gastos", false)]
    public void NavItem_IsCurrent_MatchesTheDestinationAndWhatIsInsideIt(string href, string path, bool expected)
        => Assert.Equal(expected, new NavItem(href, "x", Icons.Home).IsCurrent(path));

    [Fact]
    public void NavItems_HaveUniqueDestinations_AndTheFilledIconsExistOnlyWhereTheyShouldChange()
    {
        var all = NavItems.Main.Concat(NavItems.More).Distinct().ToList();
        Assert.Equal(all.Count, all.Select(i => i.Href).Distinct().Count());
        Assert.Equal(Icons.HomeActive, NavItems.Home.ActiveIcon);
        Assert.Equal(NavItems.Expenses.Icon, NavItems.Expenses.ActiveIcon);   // no filled variant: same icon
        Assert.DoesNotContain(NavItems.Settings, NavItems.Main);              // Settings sits at the bottom
        Assert.Contains(NavItems.Settings, NavItems.More);
    }

    // ================= NavMenu =================

    [Fact]
    public void NavMenu_SignedIn_ListsEveryDestination_AndMarksTheCurrentOne()
    {
        SignIn();
        NavigateTo("gastos/2026/11?x=1");

        var menu = Render<NavMenu>();

        var links = menu.FindAll(".sidebar__nav .sidebar__link");
        Assert.Equal(NavItems.Main.Select(i => i.Text), links.Select(l => l.GetAttribute("aria-label")));

        var current = menu.FindAll(".sidebar__link.active");
        Assert.Single(current);
        Assert.Equal("Gastos", current[0].GetAttribute("aria-label"));
        Assert.Equal("page", current[0].GetAttribute("aria-current"));
        Assert.All(menu.FindAll(".sidebar__nav .sidebar__link:not(.active)"), l => Assert.Null(l.GetAttribute("aria-current")));
    }

    [Fact]
    public void NavMenu_TheHomeLinkIsOnlyCurrentOnTheHomePage_AndUsesTheFilledIcon()
    {
        SignIn();
        NavigateTo("");

        var menu = Render<NavMenu>();

        var home = menu.Find(".sidebar__link.active");
        Assert.Equal("Inicio", home.GetAttribute("aria-label"));
        Assert.NotEmpty(home.QuerySelectorAll(".bi-house-fill"));
        Assert.Single(menu.FindAll(".sidebar__link.active"));
    }

    [Fact]
    public void NavMenu_EveryLinkHasAVisibleLabelAndATitle_ForTheCollapsedState()
    {
        SignIn();
        var menu = Render<NavMenu>();

        foreach (var link in menu.FindAll("a.sidebar__link"))
        {
            Assert.False(string.IsNullOrWhiteSpace(link.GetAttribute("title")));
            Assert.False(string.IsNullOrWhiteSpace(link.QuerySelector(".sidebar__label")?.TextContent));
        }
    }

    [Fact]
    public void NavMenu_TheBottomHasSettings_AndTheUserMenuWithAccountAndSignOut()
    {
        SignIn("ana@example.com");
        var menu = Render<NavMenu>();

        var bottom = menu.Find(".sidebar__bottom");
        Assert.Equal("Configuración", bottom.QuerySelector("a.sidebar__link")!.GetAttribute("aria-label"));

        var trigger = bottom.QuerySelector(".dropdown-app__trigger")!;
        Assert.Equal("Menú de ana@example.com", trigger.GetAttribute("aria-label"));
        Assert.Contains("ana@example.com", trigger.TextContent);

        menu.Find(".dropdown-app__trigger").Click();
        var items = menu.FindAll("[role=menuitem]").Select(i => i.TextContent.Trim());
        Assert.Equal(new[] { "Mi cuenta", "Cerrar sesión" }, items);
        Assert.Contains("dropdown-app__menu--up", menu.Find("[role=menu]").ClassName);   // opens above the button
    }

    [Fact]
    public void NavMenu_SigningOutSubmitsTheLogoutForm_ThroughTheScript()
    {
        SignIn();
        var menu = Render<NavMenu>();
        var form = menu.Find("form#logout-form");
        Assert.Equal("post", form.GetAttribute("method"), ignoreCase: true);
        Assert.EndsWith("Account/Logout", form.GetAttribute("action"));

        menu.Find(".dropdown-app__trigger").Click();
        menu.FindAll("[role=menuitem]").Single(i => i.TextContent.Contains("Cerrar sesión")).Click();

        Assert.Equal("logout-form", Assert.Single(_module.Invocations["submitForm"]).Arguments[0]);
    }

    [Fact]
    public void NavMenu_TheCollapseButton_ReflectsTheState_AndOnlyExistsWhereThePageIsInteractive()
    {
        SignIn();
        var toggled = 0;

        var expanded = Render<NavMenu>(p => p.Add(m => m.OnToggle, EventCallback.Factory.Create(this, () => toggled++)));
        var button = expanded.Find("button.sidebar__toggle");
        Assert.Equal("true", button.GetAttribute("aria-expanded"));
        Assert.Equal("Colapsar barra lateral", button.GetAttribute("aria-label"));
        button.Click();
        Assert.Equal(1, toggled);

        var collapsed = Render<NavMenu>(p => p.Add(m => m.Collapsed, true));
        Assert.Equal("false", collapsed.Find("button.sidebar__toggle").GetAttribute("aria-expanded"));
        Assert.Equal("Expandir barra lateral", collapsed.Find("button.sidebar__toggle").GetAttribute("aria-label"));
    }

    [Fact]
    public void NavMenu_OnAStaticPage_HasNoCollapseButton_BecauseItWouldDoNothing()
    {
        SignIn();
        SetRendererInfo(new RendererInfo("Static", false));

        Assert.Empty(Render<NavMenu>().FindAll(".sidebar__toggle"));
    }

    [Fact]
    public void NavMenu_SignedOut_OffersLoginAndRegisterOnly()
    {
        SignedOut();
        var menu = Render<NavMenu>();

        var links = menu.FindAll(".sidebar__link").Select(l => l.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "Iniciar sesión", "Registrarse" }, links);
        Assert.Empty(menu.FindAll(".sidebar__bottom"));
        Assert.Empty(menu.FindAll("form#logout-form"));
    }

    // ================= BottomNav =================

    [Fact]
    public void BottomNav_HasHomeExpensesNewExpensePlanAndMore_InThatOrder()
    {
        SignIn();
        var nav = Render<BottomNav>();

        var entries = nav.FindAll(".bottom-nav > *").Select(e => (e.GetAttribute("aria-label") ?? e.TextContent).Trim()).ToList();
        Assert.Equal(new[] { "Inicio", "Gastos", "Nuevo gasto", "Plan", "Más" }, entries);
        Assert.Equal("gastos?nuevo=1", nav.Find("a.bottom-nav__add").GetAttribute("href"));
    }

    [Fact]
    public void BottomNav_MarksTheCurrentPage()
    {
        SignIn();
        NavigateTo("plan");

        var nav = Render<BottomNav>();

        var current = nav.FindAll(".bottom-nav__item.active");
        Assert.Single(current);
        Assert.Equal("Plan", current[0].TextContent.Trim());
        Assert.Equal("page", current[0].GetAttribute("aria-current"));
    }

    [Fact]
    public void BottomNav_More_OpensASheetWithTheRestOfTheDestinations_AndSignOut()
    {
        SignIn();
        var nav = Render<BottomNav>();
        Assert.Empty(nav.FindAll("[role=dialog]"));

        nav.FindAll("button.bottom-nav__item").Single(b => b.TextContent.Contains("Más")).Click();

        var sheet = nav.Find("[role=dialog]");
        Assert.Contains("overlay--sheet", nav.Find(".overlay").ClassName);
        Assert.Equal(
            NavItems.More.Select(i => i.Text).Append("Cerrar sesión"),
            sheet.QuerySelectorAll(".sheet-list__item").Select(i => i.TextContent.Trim()));
        Assert.Equal("Más", sheet.QuerySelector(".overlay__title")!.TextContent);
    }

    [Fact]
    public void BottomNav_ChoosingADestinationInTheSheet_ClosesIt()
    {
        SignIn();
        var nav = Render<BottomNav>();
        nav.FindAll("button.bottom-nav__item").Single(b => b.TextContent.Contains("Más")).Click();

        nav.FindAll("a.sheet-list__item").Single(a => a.TextContent.Contains("Metas")).Click();

        Assert.Empty(nav.FindAll("[role=dialog]"));
    }

    [Fact]
    public void BottomNav_SignOutFromTheSheet_SubmitsTheLogoutForm()
    {
        SignIn();
        var nav = Render<BottomNav>();
        nav.FindAll("button.bottom-nav__item").Single(b => b.TextContent.Contains("Más")).Click();

        nav.FindAll("button.sheet-list__item").Single(b => b.TextContent.Contains("Cerrar sesión")).Click();

        Assert.Equal("logout-form", _module.Invocations["submitForm"].Last().Arguments[0]);
        Assert.Empty(nav.FindAll("[role=dialog]"));
    }

    [Fact]
    public void BottomNav_SignedOut_ShowsNothing()
    {
        SignedOut();
        Assert.Empty(Render<BottomNav>().FindAll(".bottom-nav"));
    }
}
