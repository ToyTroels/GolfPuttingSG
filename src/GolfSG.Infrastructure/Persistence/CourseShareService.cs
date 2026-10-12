using System.Security.Cryptography;
using System.Text.Json;
using GolfSG.Application.Courses;
using GolfSG.Core.Models;

namespace GolfSG.Infrastructure.Persistence;

/// <summary>Portable course definitions, without sessions or player results.</summary>
public sealed class CourseShareService(CoursePracticeService courses, string imageDirectory)
{
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private const int MaxPackageBytes = 15 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private sealed record Package(int SchemaVersion, PracticeCourse Course, byte[] Image);

    public async Task ExportAsync(PracticeCourse course, Stream destination)
    {
        CoursePracticeService.ValidateCourse(course);
        var path = Path.Combine(imageDirectory, course.ImageFile);
        if (new FileInfo(path).Length > MaxImageBytes) throw new ArgumentException("Billedet er for stort til deling (maks. 10 MB).");
        var image = await File.ReadAllBytesAsync(path);
        ValidateImage(image);
        await JsonSerializer.SerializeAsync(destination, new Package(1, course, image), Options);
    }

    public async Task<PracticeCourse> ImportAsync(Stream source)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + count > MaxPackageBytes) throw new ArgumentException("Banefilen er for stor (maks. 15 MB).");
            await buffer.WriteAsync(chunk.AsMemory(0, count));
        }
        Package package;
        try
        {
            package = JsonSerializer.Deserialize<Package>(buffer.ToArray(), Options)
                ?? throw new ArgumentException("Banefilen er tom.");
        }
        catch (JsonException) { throw new ArgumentException("Filen er ikke en gyldig GolfSG-banefil."); }
        if (package.SchemaVersion != 1) throw new ArgumentException("Banefilens version understøttes ikke.");
        CoursePracticeService.ValidateCourse(package.Course);
        var extension = ValidateImage(package.Image);
        var imageHash = Convert.ToHexString(SHA256.HashData(package.Image)).ToLowerInvariant();
        // Content identity keeps repeated imports idempotent without replacing locally edited courses.
        var identity = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(package, Options))).ToLowerInvariant();
        var course = package.Course with { Id = "shared-" + identity, ImageFile = imageHash + extension };
        var existing = (await courses.LoadAsync()).Courses.FirstOrDefault(c => c.Id == course.Id);
        if (existing is not null) return existing;
        Directory.CreateDirectory(imageDirectory);
        var imagePath = Path.Combine(imageDirectory, course.ImageFile);
        var temporary = imagePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, package.Image);
            File.Move(temporary, imagePath, true);
            return await courses.ImportCourseAsync(course);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string ValidateImage(byte[]? image)
    {
        if (image is null || image.Length == 0 || image.Length > MaxImageBytes)
            throw new ArgumentException("Banefilen skal indeholde et billede på højst 10 MB.");
        if (image.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (image.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) return ".jpg";
        throw new ArgumentException("Banebilledet skal være JPEG eller PNG.");
    }
}
