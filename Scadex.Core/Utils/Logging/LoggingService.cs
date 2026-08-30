using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Scadex.Core.Utils.Logging;

public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger;
    }
    public void LogInfo(string message, [CallerFilePath] string filePath = "", [CallerMemberName] string memeberName = "", [CallerLineNumber] int lineNumber = 0)
    {
        _logger.LogInformation("[{FilePath}::{MemberName}::{LineNumber}] {Message}", filePath, memeberName, lineNumber, message);
    }
    public void LogWarning(string message, [CallerFilePath] string filePath = "", [CallerMemberName] string memeberName = "", [CallerLineNumber] int lineNumber = 0)
    {
        _logger.LogWarning("[{FilePath}::{MemberName}::{LineNumber}] {Message}", filePath, memeberName, lineNumber, message);
    }
    public void LogError(string message, Exception? exception = null, [CallerFilePath] string filePath = "", [CallerMemberName] string memeberName = "", [CallerLineNumber] int lineNumber = 0)
    {
        _logger.LogError(exception, "[{FilePath}::{MemberName}::{LineNumber}] {Message}", filePath, memeberName, lineNumber, message);
    }
}