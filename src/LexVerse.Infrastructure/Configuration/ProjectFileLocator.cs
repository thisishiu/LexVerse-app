namespace LexVerse.Infrastructure.Configuration;

public static class ProjectFileLocator
{
    public static string? FindUpward(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static string RequireUpward(string fileName)
    {
        return FindUpward(fileName)
            ?? throw new FileNotFoundException($"Could not find required project file '{fileName}' above {AppContext.BaseDirectory}.");
    }
}
