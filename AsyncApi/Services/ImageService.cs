using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AsyncApi.Services;

public sealed class ImageService(
    ILogger<ImageService> logger,
    StorageService storageService)
{
    // A generálandó thumbnail méretek pixelben (szélesség); más osztályok is hivatkoznak rá
    public static readonly int[] ThumbnailWidths = [32, 64, 128, 256, 512, 1024];

    // Csak ezeket a fájlkiterjesztéseket és MIME típusokat fogadjuk el feltöltéskor
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif"];
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/png", "image/gif"];

    // Ellenőrzi, hogy a feltöltött fájl valóban egy elfogadott képformátum-e
    public bool IsValidImage(IFormFile file)
    {
        if (file.Length == 0) return false;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return AllowedExtensions.Contains(extension) && AllowedMimeTypes.Contains(file.ContentType);
    }

    // Elmenti az eredeti képet a temp mappába; a controller hívja feltöltés után
    public async Task<string> SaveOriginalImageAsync(IFormFile file, string folderPath, string fileName)
    {
        if (!IsValidImage(file)) throw new ArgumentException("Invalid image file.", nameof(file));

        Directory.CreateDirectory(folderPath);

        var originalFilePath = Path.Combine(folderPath, fileName);
        using var stream = new FileStream(originalFilePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return originalFilePath;
    }

    // A háttérszolgáltatás hívja: thumbnail-eket generál, feltölti MinIO-ra, törli a temp mappát
    // Visszaadja az eredeti kép méreteit, hogy a DB-be is visszaírhassuk
    public async Task<(int Width, int Height)> ProcessAndUploadAsync(string id, string originalFilePath, string folderPath)
    {
        logger.LogInformation("Starting image processing for {ImageId}", id);

        try
        {
            var (width, height) = await GenerateThumbnailsAsync(originalFilePath, folderPath, id);
            await storageService.UploadDirectoryAsync(folderPath, id, StorageBucket.Images);
            Directory.Delete(folderPath, recursive: true);

            logger.LogInformation("Image processing completed for {ImageId}", id);
            return (width, height);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image processing failed for {ImageId}", id);
            throw;
        }
    }

    // Létrehozza az összes thumbnail méretet az eredeti képből; a height=0 megtartja az arányokat
    // Visszaadja az eredeti kép méreteit
    private static async Task<(int Width, int Height)> GenerateThumbnailsAsync(string originalFilePath, string folderPath, string id)
    {
        var extension = Path.GetExtension(originalFilePath);

        using var image = await Image.LoadAsync(originalFilePath);
        var (width, height) = (image.Width, image.Height);

        foreach (var w in ThumbnailWidths)
        {
            var thumbnailPath = Path.Combine(folderPath, $"{id}_w{w}{extension}");
            using var resized = image.Clone(x => x.Resize(w, 0));
            await resized.SaveAsync(thumbnailPath);
        }

        return (width, height);
    }

    // Átméretezi a megadott képet az összes standard szélességre; videó thumbnail-ek is használják
    public static async Task GenerateResizedCopiesAsync(Image image, string folderPath, string baseName)
    {
        foreach (var width in ThumbnailWidths)
        {
            var path = Path.Combine(folderPath, $"{baseName}_w{width}.jpg");
            using var resized = image.Clone(x => x.Resize(width, 0));
            await resized.SaveAsync(path);
        }
    }

    public static async Task GenerateResizedCopiesAsync(string sourcePath, string folderPath, string baseName)
    {
        using var image = await Image.LoadAsync(sourcePath);
        await GenerateResizedCopiesAsync(image, folderPath, baseName);
    }
}
