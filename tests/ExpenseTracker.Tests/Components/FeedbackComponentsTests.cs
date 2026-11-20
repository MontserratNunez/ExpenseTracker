using Bunit;
using ExpenseTracker.Web.Components.Shared;
using ExpenseTracker.Web.Components.UI;
using ExpenseTracker.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Tests.Components;

/// <summary>Skeleton, EmptyState, ProgressBar, InlineAlert and the toasts with an action.</summary>
public class FeedbackComponentsTests : UiTestContext
{
    // ================= ProgressBar =================

    [Fact]
    public void ProgressBar_SingleSegment_IsClampedAndDescribedForScreenReaders()
    {
        var bar = Render<ProgressBar>(p => p.Add(b => b.Percent, 140m).Add(b => b.Label, "40% pagado"));

        var root = bar.Find("[role=progressbar]");
        Assert.Equal("100", root.GetAttribute("aria-valuenow"));
        Assert.Equal("40% pagado", root.GetAttribute("aria-label"));
        Assert.Contains("width: 100%", bar.Find(".progress-app__segment").GetAttribute("style"));

        var negative = Render<ProgressBar>(p => p.Add(b => b.Percent, -5m).Add(b => b.Label, "x"));
        Assert.Contains("width: 0%", negative.Find(".progress-app__segment").GetAttribute("style"));
    }

    [Fact]
    public void ProgressBar_Segments_NeverAddUpToMoreThanTheWholeBar()
    {
        var segments = new List<ProgressSegment>
        {
            new() { Value = 70m, ColorVar = "--color-spent" },
            new() { Value = 50m, ColorVar = "--color-committed", Striped = true }
        };

        var bar = Render<ProgressBar>(p => p.Add(b => b.Segments, segments).Add(b => b.Label, "Gastado y pendiente"));

        var parts = bar.FindAll(".progress-app__segment");
        Assert.Equal(2, parts.Count);
        Assert.Contains("width: 70%", parts[0].GetAttribute("style"));
        Assert.Contains("width: 30%", parts[1].GetAttribute("style"));
        Assert.Contains("var(--color-committed)", parts[1].GetAttribute("style"));
        Assert.Contains("progress-app__segment--striped", parts[1].ClassName);
        Assert.Equal("100", bar.Find("[role=progressbar]").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void ProgressBar_Marker_IsOptionalAndStaysInsideTheBar()
    {
        Assert.Empty(Render<ProgressBar>(p => p.Add(b => b.Percent, 10m).Add(b => b.Label, "x")).FindAll(".progress-app__marker"));

        var bar = Render<ProgressBar>(p => p.Add(b => b.Percent, 10m).Add(b => b.Marker, 250m)
            .Add(b => b.MarkerLabel, "Estimado a fin de mes").Add(b => b.Label, "x"));

        var marker = bar.Find(".progress-app__marker");
        Assert.Contains("left: 100%", marker.GetAttribute("style"));
        Assert.Equal("Estimado a fin de mes", marker.GetAttribute("title"));
    }

    [Fact]
    public void ProgressBar_Height_IsAppliedInPixels()
        => Assert.Contains("height: 6px", Render<ProgressBar>(p => p.Add(b => b.Height, 6).Add(b => b.Label, "x"))
            .Find("[role=progressbar]").GetAttribute("style"));

    // ================= Skeleton =================

    [Fact]
    public void Skeleton_AnnouncesLoadingOnce_AndHidesTheShapesFromScreenReaders()
    {
        var skeleton = Render<Skeleton>(p => p.Add(s => s.Lines, 3));

        var root = skeleton.Find("[role=status]");
        Assert.Equal("true", root.GetAttribute("aria-busy"));
        Assert.Equal("Cargando...", skeleton.Find(".sr-only").TextContent);
        var shapes = skeleton.FindAll(".skeleton");
        Assert.Equal(3, shapes.Count);
        Assert.All(shapes, s => Assert.Equal("true", s.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void Skeleton_LastLineOfSeveralIsShorter_AndACircleUsesTheCircleClass()
    {
        var lines = Render<Skeleton>(p => p.Add(s => s.Lines, 2).Add(s => s.Width, "80%").Add(s => s.Height, "2rem")).FindAll(".skeleton");
        Assert.Contains("width: 80%", lines[0].GetAttribute("style"));
        Assert.Contains("height: 2rem", lines[0].GetAttribute("style"));
        Assert.Contains("width: 60%", lines[1].GetAttribute("style"));

        var single = Render<Skeleton>(p => p.Add(s => s.Lines, 1).Add(s => s.Width, "40px").Add(s => s.Height, "40px").Add(s => s.Circle, true));
        Assert.Contains("skeleton--circle", single.Find(".skeleton").ClassName);
        Assert.Contains("width: 40px", single.Find(".skeleton").GetAttribute("style"));
    }

    // ================= EmptyState =================

    [Fact]
    public void EmptyState_ShowsIconTitleDescriptionAndAction()
    {
        var empty = Render<EmptyState>(p => p
            .Add(e => e.Icon, Icons.EmptyGoals)
            .Add(e => e.Title, "Aún no tienes metas")
            .Add(e => e.Description, "Crea la primera.")
            .Add(e => e.Action, (RenderFragment)(b => b.AddMarkupContent(0, "<button>Nueva meta</button>"))));

        Assert.Equal("Aún no tienes metas", empty.Find(".empty-state__title").TextContent);
        Assert.Equal("Crea la primera.", empty.Find(".empty-state__description").TextContent);
        Assert.NotEmpty(empty.FindAll(".bi-piggy-bank"));
        Assert.Equal("Nueva meta", empty.Find(".empty-state__action button").TextContent);
    }

    [Fact]
    public void EmptyState_WithoutDescriptionOrAction_RendersOnlyWhatWasGiven()
    {
        var empty = Render<EmptyState>(p => p.Add(e => e.Title, "Nada por aquí"));

        Assert.Empty(empty.FindAll(".empty-state__description"));
        Assert.Empty(empty.FindAll(".empty-state__action"));
        Assert.NotEmpty(empty.FindAll(".bi-inbox"));
    }

    // ================= InlineAlert =================

    [Theory]
    [InlineData(InlineAlert.AlertTone.Danger, "alert", "bi-exclamation-circle")]
    [InlineData(InlineAlert.AlertTone.Warning, "status", "bi-exclamation-triangle")]
    [InlineData(InlineAlert.AlertTone.Info, "status", "bi-info-circle")]
    [InlineData(InlineAlert.AlertTone.Success, "status", "bi-check-circle")]
    public void InlineAlert_UsesAnIconAndARolePerTone(InlineAlert.AlertTone tone, string role, string icon)
    {
        var alert = Render<InlineAlert>(p => p.Add(a => a.Tone, tone).AddChildContent("Texto"));

        var root = alert.Find(".inline-alert");
        Assert.Equal(role, root.GetAttribute("role"));
        Assert.Contains($"inline-alert--{tone.ToString().ToLower()}", root.ClassName);
        Assert.NotEmpty(alert.FindAll("." + icon));
        Assert.Equal("Texto", alert.Find(".inline-alert__text").TextContent);
        Assert.Empty(alert.FindAll("button"));   // no close button unless asked for
    }

    [Fact]
    public void InlineAlert_WithOnDismiss_ShowsACloseButton_AndHiddenRendersNothing()
    {
        var dismissed = 0;
        var alert = Render<InlineAlert>(p => p
            .Add(a => a.OnDismiss, EventCallback.Factory.Create(this, () => dismissed++))
            .AddChildContent("Texto"));

        alert.Find("button[aria-label='Cerrar aviso']").Click();
        Assert.Equal(1, dismissed);

        Assert.Empty(Render<InlineAlert>(p => p.Add(a => a.Visible, false).AddChildContent("x")).FindAll(".inline-alert"));
    }

    [Fact]
    public void ErrorAlert_ShowsTheMessageAsADangerAlert_AndClosingClearsIt()
    {
        string? current = "No se pudo guardar.";
        var alert = Render<ErrorAlert>(p => p
            .Add(a => a.Message, current)
            .Add(a => a.MessageChanged, EventCallback.Factory.Create<string?>(this, value => current = value)));

        Assert.Equal("No se pudo guardar.", alert.Find(".inline-alert--danger .inline-alert__text").TextContent);

        alert.Find("button").Click();
        Assert.Null(current);

        Assert.Empty(Render<ErrorAlert>(p => p.Add(a => a.Message, "  ")).FindAll(".inline-alert"));
    }

    // ================= Toasts =================

    [Fact]
    public void ToastHost_ShowsAToastWithAnIconPerKind_AndTheActionButtonOnlyWhenThereIsAnAction()
    {
        var toasts = Services.GetRequiredService<ToastService>();
        var host = Render<ToastHost>();

        toasts.ShowSuccess("Gasto guardado.");
        toasts.ShowError("Falló.");
        toasts.ShowInfo("Aviso.");
        toasts.Show(ToastKind.Success, "Gasto eliminado.", "Deshacer", () => Task.CompletedTask);

        host.WaitForState(() => host.FindAll(".toast-app").Count == 4);
        Assert.NotEmpty(host.FindAll(".toast-app--success .bi-check-circle"));
        Assert.NotEmpty(host.FindAll(".toast-app--error .bi-x-circle"));
        Assert.NotEmpty(host.FindAll(".toast-app--info .bi-info-circle"));
        Assert.Equal("polite", host.Find(".toast-host").GetAttribute("aria-live"));

        var withAction = host.FindAll(".toast-app").Single(t => t.TextContent.Contains("Gasto eliminado."));
        Assert.Equal("Deshacer", withAction.QuerySelector(".toast-app__action")!.TextContent);
        Assert.Single(host.FindAll(".toast-app__action"));
    }

    [Fact]
    public void ToastAction_RunsOnce_AndClosesTheToast()
    {
        var toasts = Services.GetRequiredService<ToastService>();
        var host = Render<ToastHost>();
        var runs = 0;

        toasts.Show(ToastKind.Success, "Gasto eliminado.", "Deshacer", () => { runs++; return Task.CompletedTask; });
        host.WaitForState(() => host.FindAll(".toast-app").Count == 1);

        host.Find(".toast-app__action").Click();

        Assert.Equal(1, runs);
        host.WaitForState(() => host.FindAll(".toast-app").Count == 0);
    }

    [Fact]
    public async Task ToastService_ActionOfAnAlreadyClosedToast_DoesNothing()
    {
        var toasts = new ToastService();
        var runs = 0;
        toasts.Show(ToastKind.Success, "Gasto eliminado.", "Deshacer", () => { runs++; return Task.CompletedTask; });
        var id = toasts.Items.Single().Id;

        toasts.Dismiss(id);
        await toasts.RunActionAsync(id);

        Assert.Equal(0, runs);
    }

    [Fact]
    public async Task ToastService_RunAction_ClosesBeforeRunning_SoItCannotRunTwice()
    {
        var toasts = new ToastService();
        var runs = 0;
        toasts.Show(ToastKind.Success, "Gasto eliminado.", "Deshacer", () => { runs++; return Task.CompletedTask; });
        var id = toasts.Items.Single().Id;

        await Task.WhenAll(toasts.RunActionAsync(id), toasts.RunActionAsync(id));

        Assert.Equal(1, runs);
        Assert.Empty(toasts.Items);
    }
}
