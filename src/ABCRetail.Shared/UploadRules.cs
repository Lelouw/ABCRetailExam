namespace ABCRetail.Shared;

public static class ImageUploadRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static string? Validate(string? contentType, long length)
    {
        if (length <= 0)
        {
            return "Choose an image to upload.";
        }

        if (length > MaxBytes)
        {
            return "The image must be 5 MB or smaller.";
        }

        if (!TryGetExtension(contentType, out _))
        {
            return "Upload a JPG, PNG, or WEBP image.";
        }

        return null;
    }

    public static bool TryGetExtension(string? contentType, out string extension)
    {
        extension = contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => string.Empty
        };

        return extension.Length > 0;
    }
}

public static class ContractUploadRules
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".pdf", ".txt", ".docx", ".png", ".jpg", ".jpeg"];

    public static string? Validate(string? fileName, long length)
    {
        if (length <= 0)
        {
            return "Choose a file to upload.";
        }

        if (length > MaxBytes)
        {
            return "The file must be 10 MB or smaller.";
        }

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return "Upload a PDF, TXT, DOCX, PNG, or JPG file.";
        }

        return null;
    }
}
