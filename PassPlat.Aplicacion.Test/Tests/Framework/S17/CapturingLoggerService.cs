using CBP.Logging.Interfaces;
using CBP.Logging.Models;
using Microsoft.Extensions.Logging;
using CbpLogLevel = CBP.Logging.Models.LogLevel;

namespace PassPlat.Aplicacion.Test.Tests.Framework.S17;

/// <summary>
/// Fake de ILoggerService que captura los LogEvent emitidos por el framework
/// (JwtTokenService, EventDispatcher) para verificar la instrumentación S17.
/// </summary>
internal sealed class CapturingLoggerService : ILoggerService
{
    public List<LogEvent> LogEvents { get; } = new();
    public List<(CbpLogLevel Level, string Message, object[] Args)> PlainMessages { get; } = new();
    public List<(CbpLogLevel Level, LogEvent Event)> AllLogCalls { get; } = new();

    public void LogException(Exception exception) { }

    public void LogDebug(string message, params object[] args) => PlainMessages.Add((CbpLogLevel.Debug, message, args));
    public void LogDebug(LogEvent logEvent) => Capture(CbpLogLevel.Debug, logEvent);
    public void LogInformation(string message, params object[] args) => PlainMessages.Add((CbpLogLevel.Information, message, args));
    public void LogInformation(LogEvent logEvent) => Capture(CbpLogLevel.Information, logEvent);
    public void LogWarning(string message, params object[] args) => PlainMessages.Add((CbpLogLevel.Warning, message, args));
    public void LogWarning(LogEvent logEvent) => Capture(CbpLogLevel.Warning, logEvent);
    public void LogError(string message, params object[] args) => PlainMessages.Add((CbpLogLevel.Error, message, args));
    public void LogError(LogEvent logEvent) => Capture(CbpLogLevel.Error, logEvent);
    public void LogError(Exception exception, string message, params object[] args) => PlainMessages.Add((CbpLogLevel.Error, message, args));
    public void LogCritical(string message, params object[] args) => PlainMessages.Add((CbpLogLevel.Critical, message, args));
    public void LogCritical(LogEvent logEvent) => Capture(CbpLogLevel.Critical, logEvent);
    public void LogCritical(Exception exception, string message, params object[] args) => PlainMessages.Add((CbpLogLevel.Critical, message, args));
    public IDisposable BeginScope<TState>(TState state) => new ScopeDisposable();
    public bool IsEnabled(CbpLogLevel level) => true;
    public void Log(CbpLogLevel level, string message, params object[] args) => PlainMessages.Add((level, message, args));
    public void Log(CbpLogLevel level, LogEvent logEvent) => Capture(level, logEvent);

    public List<LogEvent> Events(string eventName) =>
        LogEvents.Where(e => e.EventName == eventName).ToList();

    private void Capture(CbpLogLevel level, LogEvent logEvent)
    {
        AllLogCalls.Add((level, logEvent));
        LogEvents.Add(logEvent);
    }

    private sealed class ScopeDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
