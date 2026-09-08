using System.Text;

namespace TeleDrive.Core.Helpers;

public static class CrashLogger
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TeleDrive",
        "logs");

    public static string LogException(Exception exception, string context)
    {
        Directory.CreateDirectory(LogDirectory);

        var fileName = $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log";
        var path = Path.Combine(LogDirectory, fileName);

        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Context: {context}");
        builder.AppendLine();
        builder.AppendLine(exception.ToString());

        File.WriteAllText(path, builder.ToString());

        return path;
    }

    public static void PruneOldLogs(int keepCount)
    {
        if (!Directory.Exists(LogDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(LogDirectory, "crash-*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(keepCount);

        foreach (var file in files)
        {
            try
            {
                file.Delete();
            }
            catch
            {
            }
        }
    }
}
