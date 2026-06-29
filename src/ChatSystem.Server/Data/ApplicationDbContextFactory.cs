using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChatSystem.Server.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var contentRootPath = Directory.Exists(Path.Combine(currentDirectory, "Pages"))
            ? currentDirectory
            : Path.Combine(currentDirectory, "src", "ChatSystem.Server");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(DatabasePath.GetConnectionString(contentRootPath))
            .Options;

        return new ApplicationDbContext(options);
    }
}
