using GolfSG.Application.Accounts;
using GolfSG.Infrastructure.Courses;

namespace GolfSG.Views;

/// <summary>One cancellable request per page; navigation cancels outstanding work.</summary>
public abstract class CourseCommunityPage : ContentPage
{
    protected readonly CourseCommunityWorkflow Community;
    protected readonly VerticalStackLayout Body = new() { Padding = 16, Spacing = 14 };
    protected ApiSession Session { get; private set; } = null!;
    private readonly Label status = new() { TextColor = GolfTheme.Colors.MutedText };
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText };
    private readonly Button cancel = CoursePracticeViews.SecondaryButton("Annuller ventetid");
    private CancellationTokenSource? pageCancellation;
    private CancellationTokenSource? requestCancellation;
    private bool busy;
    private Task currentRequest = Task.CompletedTask;

    protected CourseCommunityPage(CourseCommunityWorkflow community, string title, string id)
    {
        Community = community; Title = title; BackgroundColor = GolfTheme.Colors.PageBackground;
        CoursePracticeViews.ConfigurePage(this); this.Accessible(id, title);
        cancel.IsVisible = false; cancel.Clicked += (_, _) => requestCancellation?.Cancel();
        var retry = CoursePracticeViews.SecondaryButton("Opdater").Accessible(id + ".Refresh", "Opdater siden");
        retry.Clicked += async (_, _) => await RunAsync(LoadAsync);
        var back = CoursePracticeViews.SecondaryButton("Tilbage");
        back.Clicked += async (_, _) => { requestCancellation?.Cancel(); await this.RunNavigationOnceAsync(() => Navigation.PopAsync()); };
        var footer = new VerticalStackLayout { Padding = 16, Spacing = 8, Children = { status, error, cancel, retry, back } };
        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { new ScrollView { Content = Body }.Row(0), footer.Row(1) }
        };
    }

    protected abstract Task LoadAsync(CancellationToken token);

    protected override async void OnAppearing()
    {
        base.OnAppearing(); pageCancellation?.Cancel(); pageCancellation?.Dispose();
        var lifetime = new CancellationTokenSource(); pageCancellation = lifetime;
        await currentRequest;
        if (ReferenceEquals(pageCancellation, lifetime) && !lifetime.IsCancellationRequested) await RunAsync(LoadAsync);
    }

    protected override void OnDisappearing()
    {
        pageCancellation?.Cancel(); base.OnDisappearing();
    }

    protected Task RunAsync(Func<CancellationToken, Task> action)
        => RunAsync(action, true);

    protected Task RunLocalAsync(Func<CancellationToken, Task> action)
        => RunAsync(action, false);

    private Task RunAsync(Func<CancellationToken, Task> action, bool authenticate)
    {
        if (busy || pageCancellation is null || pageCancellation.IsCancellationRequested) return Task.CompletedTask;
        currentRequest = RunCoreAsync(action, pageCancellation.Token, authenticate); return currentRequest;
    }

    private async Task RunCoreAsync(Func<CancellationToken, Task> action, CancellationToken pageToken, bool authenticate)
    {
        busy = true; Body.IsEnabled = false; error.Text = ""; status.Text = authenticate ? "Kontakter serveren …" : ""; cancel.IsVisible = true;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(pageToken);
        requestCancellation = cancellation;
        try
        {
            if (authenticate) Session = await Community.SessionAsync(cancellation.Token);
            await action(cancellation.Token);
        }
        catch (OperationCanceledException) { status.Text = "Handlingen blev afbrudt. Dine lokale data er bevaret."; }
        catch (Exception ex)
        {
            error.Text = ex is ArgumentException or InvalidOperationException or ApiAccountException ? ex.Message :
                "Handlingen kunne ikke fuldføres. Prøv igen; dine lokale data er bevaret.";
        }
        finally { requestCancellation = null; busy = false; Body.IsEnabled = true; cancel.IsVisible = false; if (status.Text == "Kontakter serveren …") status.Text = ""; }
    }

    protected static Label Text(string text) => new() { Text = text, TextColor = GolfTheme.Colors.Text };
    protected static Button Action(string text, string id, Func<Task> action)
    {
        var button = CoursePracticeViews.PrimaryButton(text).Accessible(id, text);
        button.Clicked += async (_, _) => await action(); return button;
    }
}
