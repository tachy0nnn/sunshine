using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Sunshine;

/// <summary>
///     Simple logger.
///     Under %localappdata%\Sunshine\Logs\ and keeps an in-memory history.
///     Usage: Logger.WriteLine("Bootstrapper::RunAsync", "starting");
///     Logger.WriteException("Bootstrapper::DownloadPackageAsync", ex);
/// </summary>
public static class Logger
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static StreamWriter? _writer;

    /// <summary>
    ///     Path to the current session log file, or null if not yet initialized.
    /// </summary>
    public static string? FilePath { get; private set; }

    /// <summary>
    ///     Call once at startup (e.g. from App.OnFrameworkInitializationCompleted).
    ///     Creates the log file and flushes any lines that were queued before init.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(Paths.Logs);

            // we need PID since rider IDE is a fucking bitch and loves to keep the previous process alive
            // so by adding PID to the name, we make everytime a unique name for logfile
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var pid = Environment.ProcessId;
            FilePath = Path.Combine(Paths.Logs, $"Sunshine_{timestamp}_{pid}.log");

            _writer = new StreamWriter(FilePath, false, Encoding.UTF8) { AutoFlush = true };

            WriteLine("Logger::Initialize", $"log file opened: {FilePath}");
            CleanOldLogs();
        }
        catch (Exception ex)
        {
            // logger init failure is non-fatal
            Console.Error.WriteLine($"[Logger] failed to initialize: {ex.Message}");
        }
    }

    /// <summary>
    ///     Writes a single informational line.
    /// </summary>
    public static void WriteLine(string identifier, string message)
    {
        var line = Format(identifier, message);
        WriteRaw(line);
    }

    /// <summary>
    ///     Writes the full exception details including inner exceptions.
    /// </summary>
    public static void WriteException(string identifier, Exception ex)
    {
        WriteLine(identifier, $"exception ({ex.GetType().Name}): {ex.Message}");

        if (!string.IsNullOrEmpty(ex.StackTrace))
            WriteRaw(ex.StackTrace);

        if (ex.InnerException is not null)
            WriteException(identifier + " [inner]", ex.InnerException);
    }

    private static string Format(string identifier, string message)
    {
        var ts = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        return $"{ts} [{identifier}] {message}";
    }

    private static void WriteRaw(string line)
    {
        // always echo to the debug output so it's visible in the IDE
        Debug.WriteLine(line);

        if (_writer is null) return;

        Lock.Wait();
        try
        {
            _writer.WriteLine(line);
        }
        catch
        {
            /* skip */
        }
        finally
        {
            Lock.Release();
        }
    }

    /// TODO: make this into a setting!!!
    /// <summary>
    ///     Deletes log files older than 7 days to avoid unbounded disk growth.
    /// </summary>
    private static void CleanOldLogs()
    {
        try
        {
            foreach (var file in new DirectoryInfo(Paths.Logs).GetFiles("*.log"))
                if (file.LastWriteTimeUtc.AddDays(7) < DateTime.UtcNow)
                {
                    file.Delete();
                    WriteLine("Logger::CleanOldLogs", $"deleted old log: {file.Name}");
                }
        }
        catch (Exception ex)
        {
            WriteLine("Logger::CleanOldLogs", $"cleanup failed: {ex.Message}");
        }
    }
}