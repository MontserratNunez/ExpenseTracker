using Bunit;
using ExpenseTracker.Web.Components.UI;
using ExpenseTracker.Web.Components.Shared;
using ExpenseTracker.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;

namespace ExpenseTracker.Tests.Components;

/// <summary>
/// Structure and callbacks of the overlays, and that they ask the JavaScript module for the focus trap and the
/// scroll lock when they open and close. The real focus and keyboard behavior is checked in the browser tests.
/// </summary>
public class OverlayTests : UiTestContext
{
    private readonly BunitJSModuleInterop _module;
    private int closed;

    public OverlayTests()
    {
        _module = JSInterop.SetupModule("./js/ui.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    private EventCallback OnClose => EventCallback.Factory.Create(this, () => closed++);

    private IRenderedComponent<AppModal> RenderModal(bool open = true, RenderFragment? footer = null, OverlayWidth width = OverlayWidth.Md)
        => Render<AppModal>(p => p
            .Add(m => m.IsOpen, open)
            .Add(m => m.Title, "Un título")
            .Add(m => m.Width, width)
            .Add(m => m.OnClose, OnClose)
            .Add(m => m.Footer, footer)
            .AddChildContent("<p>Contenido</p>"));

    private static int Count(BunitJSModuleInterop module, string identifier)
        => module.Invocations[identifier].Count;

    // ================= AppModal =================

    [Fact]
    public void Modal_Closed_RendersNothing()
    {
        Assert.Empty(RenderModal(open: false).Markup.Trim());
    }

    [Fact]
    public void Modal_Open_IsAnAccessibleDialogNamedByItsTitle()
    {
        var cut = RenderModal();

        var dialog = cut.Find("[role=dialog]");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        var titleId = dialog.GetAttribute("aria-labelledby");
        Assert.Equal("Un título", cut.Find($"#{titleId}").TextContent);
        Assert.Equal("-1", dialog.GetAttribute("tabindex")); // focusable, so Esc works even after clicking plain text
        Assert.Contains("Contenido", dialog.TextContent);
    }

    [Theory]
    [InlineData(OverlayWidth.Sm, "overlay--w-sm")]
    [InlineData(OverlayWidth.Md, "overlay--w-md")]
    [InlineData(OverlayWidth.Lg, "overlay--w-lg")]
    [InlineData(OverlayWidth.Xl, "overlay--w-xl")]
    public void Modal_Width_IsAClass(OverlayWidth width, string expected)
    {
        var overlay = RenderModal(width: width).Find(".overlay");

        Assert.Contains(expected, overlay.ClassList);
        Assert.Contains("overlay--modal", overlay.ClassList);
    }

    [Fact]
    public void Modal_WithFooter_WrapsTheContentInABody_AndPutsTheButtonsInTheFooter()
    {
        var cut = RenderModal(footer: b => b.AddMarkupContent(0, "<button>Guardar</button>"));

        Assert.Contains("Contenido", cut.Find(".overlay__body").TextContent);
        Assert.Equal("Guardar", cut.Find(".overlay__footer button").TextContent);
    }

    [Fact]
    public void Modal_WithoutFooter_PlacesTheContentAsIs_ForFormsThatWrapTheirOwnBody()
    {
        var cut = RenderModal();

        Assert.Empty(cut.FindAll(".overlay__body"));
        Assert.Empty(cut.FindAll(".overlay__footer"));
    }

    [Fact]
    public void Modal_Escape_RaisesOnClose()
    {
        var cut = RenderModal();

        cut.Find(".overlay__panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.Find(".overlay__panel").KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Equal(1, closed);
    }

    [Fact]
    public void Modal_ClickOnTheDimmedArea_Closes()
    {
        var cut = RenderModal();

        // A click inside the panel must not reach the dimmed area behind it. bUnit cannot model event bubbling,
        // so that half is checked in the browser tests; here, a click on the dimmed area closes.
        Assert.Equal(0, closed);

        cut.Find(".overlay").Click();
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Modal_CloseButton_HasAnAccessibleName_AndCloses()
    {
        var cut = RenderModal();

        cut.Find("button[aria-label=Cerrar]").Click();

        Assert.Equal(1, closed);
    }

    [Fact]
    public void Modal_Opening_TrapsTheFocus_AndLocksTheScroll_Closing_ReleasesBoth()
    {
        var cut = RenderModal(open: false);
        Assert.Equal(0, Count(_module, "trapFocus"));

        cut.Render(p => p.Add(m => m.IsOpen, true));
        cut.WaitForAssertion(() => Assert.Equal(1, Count(_module, "trapFocus")));
        Assert.Equal(true, _module.Invocations["lockScroll"].Single().Arguments[0]);

        cut.Render(p => p.Add(m => m.IsOpen, false));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, Count(_module, "releaseFocus"));
            Assert.Equal(new object?[] { true, false }, _module.Invocations["lockScroll"].Select(i => i.Arguments[0]).ToArray());
        });
    }

    [Fact]
    public async Task Modal_RemovedWhileOpen_StillReleasesTheFocusAndTheScroll()
    {
        var cut = RenderModal();
        cut.WaitForAssertion(() => Assert.Equal(1, Count(_module, "trapFocus")));

        await DisposeAsync();

        Assert.Equal(1, Count(_module, "releaseFocus"));
        Assert.Equal(false, _module.Invocations["lockScroll"].Last().Arguments[0]);
    }

    [Fact]
    public void Modal_ReRendering_WhileOpen_DoesNotTrapTwice()
    {
        var cut = RenderModal();
        cut.WaitForAssertion(() => Assert.Equal(1, Count(_module, "trapFocus")));

        cut.Render(p => p.Add(m => m.Title, "Otro título"));
        cut.Render(p => p.Add(m => m.Title, "Y otro"));

        Assert.Equal(1, Count(_module, "trapFocus"));
    }

    // ================= SidePanel =================

    [Fact]
    public void SidePanel_IsASideOverlay_WithTheSameBehavior()
    {
        var cut = Render<SidePanel>(p => p
            .Add(s => s.IsOpen, true).Add(s => s.Title, "Panel").Add(s => s.OnClose, OnClose)
            .AddChildContent("<p>Dentro</p>"));

        var overlay = cut.Find(".overlay");
        Assert.Contains("overlay--side", overlay.ClassList);
        Assert.DoesNotContain("overlay--modal", overlay.ClassList);
        Assert.Equal("dialog", cut.Find(".overlay__panel").GetAttribute("role"));

        cut.Find(".overlay__panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal(1, closed);
    }

    // ================= ConfirmDialog =================

    private int confirmed;
    private int cancelled;
    private bool? visibleAfter;

    private IRenderedComponent<ConfirmDialog> RenderConfirm(bool visible = true)
        => Render<ConfirmDialog>(p => p
            .Add(c => c.Visible, visible)
            .Add(c => c.Title, "Eliminar gasto")
            .Add(c => c.Message, "¿Eliminar este gasto?")
            .Add(c => c.ConfirmText, "Eliminar")
            .Add(c => c.VisibleChanged, EventCallback.Factory.Create<bool>(this, v => visibleAfter = v))
            .Add(c => c.OnConfirm, EventCallback.Factory.Create(this, () => confirmed++))
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => cancelled++)));

    [Fact]
    public void Confirm_ShowsTitleAndMessage_AndFocusesTheSafeButtonFirst()
    {
        var cut = RenderConfirm();

        Assert.Equal("¿Eliminar este gasto?", cut.Find(".overlay__body p").TextContent);
        var cancel = cut.FindAll(".overlay__footer button").First(b => b.TextContent.Trim() == "Cancelar");
        Assert.True(cancel.HasAttribute("data-autofocus"));
        var confirm = cut.FindAll(".overlay__footer button").First(b => b.TextContent.Trim() == "Eliminar");
        Assert.Contains("btn-app--danger", confirm.ClassList);
        Assert.False(confirm.HasAttribute("data-autofocus"));
    }

    [Fact]
    public void Confirm_Confirming_RaisesOnConfirm_AndHidesItself()
    {
        var cut = RenderConfirm();

        cut.FindAll(".overlay__footer button").First(b => b.TextContent.Trim() == "Eliminar").Click();

        Assert.Equal(1, confirmed);
        Assert.Equal(0, cancelled);
        Assert.False(visibleAfter);
    }

    [Fact]
    public void Confirm_CancelEscapeAndTheCloseButton_AllCancel()
    {
        var cut = RenderConfirm();
        cut.FindAll(".overlay__footer button").First(b => b.TextContent.Trim() == "Cancelar").Click();
        Assert.Equal(1, cancelled);

        var again = RenderConfirm();
        again.Find(".overlay__panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal(2, cancelled);

        var third = RenderConfirm();
        third.Find("button[aria-label=Cerrar]").Click();
        Assert.Equal(3, cancelled);

        Assert.Equal(0, confirmed);
    }

    // ================= Popover =================

    [Fact]
    public void Popover_Closed_RendersNothing_Open_IsALabelledDialogWithABackdrop()
    {
        Assert.Empty(Render<Popover>(p => p.Add(x => x.AriaLabel, "Elegir mes")).Markup.Trim());

        var cut = Render<Popover>(p => p
            .Add(x => x.IsOpen, true).Add(x => x.AriaLabel, "Elegir mes").Add(x => x.OnClose, OnClose)
            .AddChildContent("<p>Dentro</p>"));

        Assert.Equal("Elegir mes", cut.Find("[role=dialog]").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll(".popover-backdrop"));
        cut.WaitForAssertion(() => Assert.Equal(1, Count(_module, "trapFocus")));
    }

    [Fact]
    public void Popover_EscapeAndAClickOutside_Close()
    {
        var cut = Render<Popover>(p => p.Add(x => x.IsOpen, true).Add(x => x.AriaLabel, "x").Add(x => x.OnClose, OnClose));

        cut.Find(".popover-app").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.Find(".popover-backdrop").Click();

        Assert.Equal(2, closed);
    }

    [Fact]
    public void Popover_DoesNotLockTheScroll()
    {
        Render<Popover>(p => p.Add(x => x.IsOpen, true).Add(x => x.AriaLabel, "x"));

        Assert.Equal(0, Count(_module, "lockScroll"));
    }

    // ================= DropdownMenu =================

    private int edited;
    private int deleted;

    private IRenderedComponent<DropdownMenu> RenderMenu()
        => Render<DropdownMenu>(p => p
            .Add(m => m.AriaLabel, "Acciones de Netflix")
            .Add(m => m.Items, new[]
            {
                new DropdownMenuItem("Editar", Icons.Edit, EventCallback.Factory.Create(this, () => edited++)),
                new DropdownMenuItem("No disponible", Icons.Close, disabled: true),
                new DropdownMenuItem("Eliminar", Icons.Delete, EventCallback.Factory.Create(this, () => deleted++), danger: true)
            }));

    [Fact]
    public void Menu_Closed_ShowsOnlyTheNamedButton_ThatAnnouncesAMenu()
    {
        var cut = RenderMenu();

        var trigger = cut.Find("button.dropdown-app__trigger");
        Assert.Equal("menu", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Equal("Acciones de Netflix", trigger.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("[role=menu]"));
        cut.WaitForAssertion(() => Assert.Equal(1, Count(_module, "attachMenu")));
    }

    [Fact]
    public void Menu_Open_ListsItsOptionsWithIcons_DangerAndDisabledStates()
    {
        var cut = RenderMenu();

        cut.Find("button.dropdown-app__trigger").Click();

        Assert.Equal("true", cut.Find("button.dropdown-app__trigger").GetAttribute("aria-expanded"));
        var items = cut.FindAll("[role=menu] [role=menuitem]");
        Assert.Equal(new[] { "Editar", "No disponible", "Eliminar" }, items.Select(i => i.TextContent.Trim()));
        Assert.All(items, i => Assert.Equal("-1", i.GetAttribute("tabindex")));   // reached with the arrows, not with Tab
        Assert.All(items, i => Assert.NotNull(i.QuerySelector("i.bi")));
        Assert.True(items[1].HasAttribute("disabled"));
        Assert.Contains("dropdown-app__item--danger", items[2].ClassList);
        Assert.DoesNotContain("dropdown-app__item--danger", items[0].ClassList);
        cut.WaitForAssertion(() => Assert.Equal("first", _module.Invocations["focusMenuItem"].Single().Arguments[1]));
    }

    [Fact]
    public void Menu_ChoosingAnOption_RunsItsAction_AndClosesTheMenu()
    {
        var cut = RenderMenu();
        cut.Find("button.dropdown-app__trigger").Click();

        cut.FindAll("[role=menuitem]").First(i => i.TextContent.Contains("Eliminar")).Click();

        Assert.Equal(1, deleted);
        Assert.Equal(0, edited);
        Assert.Empty(cut.FindAll("[role=menu]"));
        cut.WaitForAssertion(() => Assert.Equal(1, Count(_module, "focusMenuTrigger"))); // focus goes back to the button first
    }

    [Fact]
    public void Menu_ADisabledOption_DoesNothing()
    {
        var cut = RenderMenu();
        cut.Find("button.dropdown-app__trigger").Click();

        cut.FindAll("[role=menuitem]").First(i => i.TextContent.Contains("No disponible")).Click();

        Assert.Equal(0, edited + deleted);
        Assert.Single(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Menu_ClickingTheButtonAgain_OrOutside_Closes()
    {
        var cut = RenderMenu();

        cut.Find("button.dropdown-app__trigger").Click();
        cut.Find("button.dropdown-app__trigger").Click();
        Assert.Empty(cut.FindAll("[role=menu]"));

        cut.Find("button.dropdown-app__trigger").Click();
        cut.Find(".dropdown-app__backdrop").Click();
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public async Task MenuCallbacks_ConnectTheKeyboardHandlerToTheComponent()
    {
        string? openedWith = null;
        bool? closedWith = null;
        var callbacks = new DropdownMenuCallbacks(f => { openedWith = f; return Task.CompletedTask; }, r => { closedWith = r; return Task.CompletedTask; });

        await callbacks.OpenFromKeyboard("last");
        await callbacks.CloseFromKeyboard(true);

        Assert.Equal("last", openedWith);
        Assert.True(closedWith);
    }

    // ================= The catalog's overlay demos =================

    [Fact]
    public void Catalog_OpensAndClosesItsModal_WithTheKeyboardHandler()
    {
        Services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(new CatalogEnvironment());
        var cut = Render<ExpenseTracker.Web.Components.Pages.Dev.UiCatalog>();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Abrir modal").Click();
        Assert.Equal("Un modal", cut.Find(".overlay__title").TextContent);

        cut.Find(".overlay__panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll(".overlay"));
    }

    private sealed class CatalogEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ExpenseTracker.Web";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
