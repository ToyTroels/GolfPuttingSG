namespace GolfSG.Views;

internal static class CoursePracticeImages
{
    internal const string SampleFile = "course-practice-sample.jpg";
    internal static string PathFor(string file) => Path.Combine(FileSystem.AppDataDirectory, "course-images", Path.GetFileName(file));

    internal static async Task EnsureSampleAsync()
    {
        var path = PathFor(SampleFile);
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var source = await FileSystem.OpenAppPackageFileAsync(SampleFile);
        await using var destination = File.Create(path);
        await source.CopyToAsync(destination);
    }
}
