namespace ChatSystem.Server.Data;

public static class DatabasePath
{
    public static string GetConnectionString(string contentRootPath)
    {
        var databasePath = GetDatabasePath(contentRootPath);
        return $"Data Source={databasePath}";
    }

    public static string GetDatabasePath(string contentRootPath)
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(contentRootPath, "..", ".."));
        var dataDirectory = Path.Combine(solutionRoot, "Data");

        Directory.CreateDirectory(dataDirectory);

        return Path.Combine(dataDirectory, "chat.db");
    }
}
