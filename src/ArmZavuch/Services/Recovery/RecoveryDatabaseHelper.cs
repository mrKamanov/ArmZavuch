using Microsoft.Data.Sqlite;

namespace ArmZavuch.Services.Recovery;

internal static class RecoveryDatabaseHelper
{
    public static void ReplaceDatabase(string targetPath, string sourcePath)
    {
        SqliteConnection.ClearAllPools();
        DeleteDatabaseFiles(targetPath);
        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    public static void DeleteDatabaseFiles(string dbPath)
    {
        foreach (var path in SidecarPaths(dbPath))
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static IEnumerable<string> SidecarPaths(string dbPath) =>
    [
        dbPath,
        dbPath + "-wal",
        dbPath + "-shm"
    ];
}
