using NEXT.Helpers;

namespace NEXT.Services;

public interface IFileStorageService
{
    Task<ServiceResult<string>> SaveImageAsync(IFormFile file, string folder, CancellationToken ct = default);
    void Delete(string? relativePath);
}

public class FileStorageService : IFileStorageService
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedFolders = new() { "avatars", "covers", "posts", "messages" };
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly string _uploadsRoot;

    public FileStorageService(IWebHostEnvironment env)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
    }

    public async Task<ServiceResult<string>> SaveImageAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        if (!AllowedFolders.Contains(folder)) throw new ArgumentException("Unknown upload folder.", nameof(folder));
        if (file.Length == 0) return ServiceResult<string>.Fail("The selected file is empty.");
        if (file.Length > MaxBytes) return ServiceResult<string>.Fail("Images must be 5 MB or smaller.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return ServiceResult<string>.Fail("Only JPG, PNG, GIF and WebP images are allowed.");

        // Don't trust the extension alone: check the file's real signature too.
        var header = new byte[12];
        await using (var peek = file.OpenReadStream())
        {
            var read = await peek.ReadAsync(header.AsMemory(0, header.Length), ct);
            if (read < 12 || !MatchesSignature(ext, header))
                return ServiceResult<string>.Fail("That file doesn't look like a valid image.");
        }

        var dir = Path.Combine(_uploadsRoot, folder);
        Directory.CreateDirectory(dir);

        // Random server-side name: the client's file name is never used.
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var target = new FileStream(Path.Combine(dir, name), FileMode.CreateNew))
        {
            await file.CopyToAsync(target, ct);
        }

        return ServiceResult<string>.Ok($"/uploads/{folder}/{name}");
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/")) return;

        var full = Path.GetFullPath(Path.Combine(_uploadsRoot, relativePath["/uploads/".Length..]));
        if (!full.StartsWith(_uploadsRoot + Path.DirectorySeparatorChar)) return; // path traversal guard

        try { if (File.Exists(full)) File.Delete(full); }
        catch (IOException) { /* a leftover file is harmless */ }
    }

    private static bool MatchesSignature(string ext, byte[] h) => ext switch
    {
        ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A,
        ".gif" => h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38,
        ".webp" => h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
        _ => false
    };
}
