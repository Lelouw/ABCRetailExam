namespace ABCRetail.Shared;

public static class LocalFileShare
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ABCRetailExam");

    public static bool IsEmulator(string? connectionString) =>
        !string.IsNullOrWhiteSpace(connectionString)
        && (connectionString.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("devstoreaccount1", StringComparison.OrdinalIgnoreCase));

    public static string GetDirectory(string shareName)
    {
        var directory = Path.GetFullPath(Path.Combine(Root, shareName));
        var root = Path.GetFullPath(Root) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("That file share name is not allowed.");
        }

        return directory;
    }

    public static string GetPath(string shareName, string fileName)
    {
        if (!FileNameSanitizer.IsSafeFileName(fileName))
        {
            throw new InvalidOperationException("That file name is not allowed.");
        }

        var directory = GetDirectory(shareName);
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        var prefix = directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("That file name is not allowed.");
        }

        return path;
    }
}

public static class FileNameSanitizer
{
    public static string Sanitize(string? fileName, string fallback = "file")
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var cleaned = new string(name.Where(IsAllowed).ToArray());
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned is "." or "..")
        {
            return fallback;
        }

        return cleaned.Length <= 80 ? cleaned : cleaned[..80];
    }

    public static bool IsSafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name == Path.GetFileName(name)
            && name.IndexOfAny(['/', '\\']) < 0
            && !name.Contains("..", StringComparison.Ordinal);
    }

    private static bool IsAllowed(char character) =>
        char.IsLetterOrDigit(character) || character is '.' or '-' or '_';
}
