using GolfSG.Admin.Preview;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.FileProviders;
using System.Net;
using GolfSG.Application.Server;

// Separate preview process: no login handlers, credentials, database connection,
// or API client. Bind explicitly to loopback, regardless of ASPNETCORE_URLS.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(7247));
builder.Services.AddRazorComponents();
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
        context.Request.Host.Host is not ("localhost" or "127.0.0.1" or "[::1]"))
    {
        context.Response.StatusCode = 403;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    await next(context);
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,
        "../../src/GolfSG.Admin/wwwroot")))
});
app.MapGet("/", () => new RazorComponentResult<PreviewPage>());
app.MapGet("/my", (HttpContext context) =>
{
    var section = context.Request.Query["section"].ToString();
    if (section == "") section = "rounds";
    var offset = 0;
    var rawOffset = context.Request.Query["offset"].ToString();
    if (section is not ("rounds" or "courses" or "games") || (rawOffset != "" && !int.TryParse(rawOffset, out offset)) || offset is < 0 or > 100000) return (IResult)Results.BadRequest();
    var items = Enumerable.Range(1, 36).Skip(offset).Take(26).Select(i => new PersonalDataItem($"my-sample-{section}-{i:000}",
        section switch { "rounds" => $"Saved practice round {i:000}", "courses" => $"My putting course {i:000}", _ => $"Weekend practice {i:000}" },
        section switch { "rounds" => "Synced", "courses" => "Published", _ => "Open" }, DateTimeOffset.Parse("2026-10-17T09:30:00Z"),
        section switch { "rounds" => "Your saved round history", "courses" => "9 positions · 18 attempts", _ => "You joined this game" }, section == "games" ? 1.25 : null)).ToArray();
    return new RazorComponentResult< GolfSG.Admin.Components.MyDataDocument>(new { IsPreview = true, Section = section,
        Page = new PersonalDataPage(section, items.Take(25).ToArray(), offset, items.Length > 25), Offset = offset, Username = "sample_player_001" });
});
app.MapGet("/my/performance", (HttpContext context) => new RazorComponentResult<GolfSG.Admin.Components.PersonalPerformanceDocument>(new { Preferences = GolfSG.Admin.Components.DashboardPreferences.Read(context, "sample-preview"), Data = SamplePerformance.Overview, IsPreview = true }));
app.MapGet("/my/rounds/{id}", (string id) => new RazorComponentResult<GolfSG.Admin.Components.PersonalPerformanceDocument>(new { Detail = SamplePerformance.Detail(id), RoundPage = true, IsPreview = true }));
static bool SampleAccount(string id, out int number)
{
    number = 0;
    return id.StartsWith("sample-account-", StringComparison.Ordinal) && int.TryParse(id[15..], out number) && number is >= 1 and <= 62;
}
app.MapGet("/users/{id}/reset-password", (string id, HttpContext context) =>
{
    if (!SampleAccount(id, out var number)) return (IResult)Results.NotFound();
    return new RazorComponentResult<PreviewPage>(new { ResetUserId = id, ResetUsername = $"sample_player_{number:000}", ResetSuccess = context.Request.Query["result"] == "preview" });
});
app.MapPost("/users/{id}/reset-password/submit", async (string id, HttpContext context) =>
{
    if (!SampleAccount(id, out var number)) return (IResult)Results.NotFound();
    if (context.Request.ContentLength is null or > 4096) return Results.BadRequest();
    var form = await context.Request.ReadFormAsync();
    if (form["confirmUsername"] != $"sample_player_{number:000}" || form["confirmed"] != "true" ||
        form.ContainsKey("newPassword") || form.ContainsKey("confirmPassword")) return Results.BadRequest();
    return Results.Redirect($"/users/{id}/reset-password?result=preview");
});
app.MapGet("/{section:regex(^(users|courses|games)$)}", (string section, HttpContext context) =>
{
    if (section is not ("users" or "courses" or "games")) return (IResult)Results.NotFound();
    var search = context.Request.Query["search"].ToString();
    var rawOffset = context.Request.Query["offset"].ToString();
    var offset = 0;
    if (search.Length > 100 || (rawOffset.Length > 0 && !int.TryParse(rawOffset, out offset)) || offset is < 0 or > 100000)
        return Results.BadRequest();
    search = search.Trim();
    var rows = Enumerable.Range(1, 62).Select(i => section switch
    {
        "users" => new AdminDirectoryRow($"sample-account-{i:000}", $"sample_player_{i:000}", "—", i % 7 == 0 ? "Locked" : "Active"),
        "courses" => new AdminDirectoryRow($"sample-course-{i:000}", $"Sample practice course {i:000}", $"Sample coach {i % 4 + 1}",
            i % 3 == 0 ? "Unpublished" : "Published", 6 + i % 5, 12 + i % 10, UpdatedAt: DateTimeOffset.Parse("2026-10-17T08:00:00Z")),
        _ => new AdminDirectoryRow($"sample-game-{i:000}", $"Sample weekend game {i:000}", $"Sample host {i % 4 + 1}",
            i % 3 == 0 ? "Closed" : "Open", Participants: 8 + i % 10, Submitted: i % 8, ScoringVersion: "sample-baseline-v1",
            UpdatedAt: DateTimeOffset.Parse("2026-10-17T08:00:00Z"))
    }).Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || r.Owner.Contains(search, StringComparison.OrdinalIgnoreCase) || r.Id == search)
        .Skip(offset).Take(26).ToArray();
    var page = new AdminDirectoryPage(section, rows.Take(25).ToArray(), rows.Length > 25, offset, 25, search);
    return new RazorComponentResult<PreviewPage>(new { Section = section, Page = page, Search = search, Offset = offset });
});
app.Run();
