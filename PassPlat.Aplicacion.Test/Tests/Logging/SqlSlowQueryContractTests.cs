using CBP.Data.Asynchronous;
using CBP.Data.Synchronous;
using CBP.Data.Utilities.Services;
using CBP.Logging;
using CBP.Logging.Configuration;
using CBP.Logging.Interfaces;
using CBP.Logging.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using System.Data;
using System.Data.Common;

namespace PassPlat.Aplicacion.Test.Tests.Logging;

/// <summary>
/// Contract tests S37.2 (Gate G-4) del emisor <c>Sql_SlowQuery</c>.
/// Valida: emisión correcta (EventName/Scope/Category/Source/Operation/elapsedMs),
/// ausencia de parámetros/valores, boundary del umbral y parity Async ≈ Sync.
/// </summary>
public class SqlSlowQueryContractTests
{
    private const string FastSp = "SP_Seguridad_Login";
    private const string FastText = "SELECT TOP 10 NomUsuario FROM Usuarios";

    private static ILoggerService CaptureLogger(out List<LogEvent> emitted)
    {
        var sink = new List<LogEvent>();
        emitted = sink;
        var mock = new Mock<ILoggerService>();
        mock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        mock.Setup(l => l.LogWarning(It.IsAny<LogEvent>()))
            .Callback<LogEvent>(e => sink.Add(e));
        return mock.Object;
    }

    private static LoggingOptions Threshold(int ms) => new() { SqlSlowQueryThresholdMs = ms };

    private static SqlSlowQueryInterceptor CreateInterceptor(
        ILoggerService logger, LoggingOptions options) =>
        new(logger, Microsoft.Extensions.Options.Options.Create(options));

    private static RawQueryRepositoryAsync CreateRawQueryAsync(
        ILoggerService logger, LoggingOptions options) =>
        new(new DbContext(new DbContextOptions<DbContext>()), logger,
            Microsoft.Extensions.Options.Options.Create(options));

    private static RawQueryRepositorySync CreateRawQuerySync(
        ILoggerService logger, LoggingOptions options) =>
        new(new DbContext(new DbContextOptions<DbContext>()), logger,
            Microsoft.Extensions.Options.Options.Create(options));

    private static DbCommand CreateCommand(string commandText)
    {
        var cmd = new Mock<DbCommand>();
        cmd.Setup(c => c.CommandText).Returns(commandText);
        return cmd.Object;
    }

    private static void AssertCanonicalContract(LogEvent evt, double expectedMs)
    {
        var p = evt.Properties;
        Assert.Equal("Sql_SlowQuery", evt.EventName);
        Assert.Equal(LoggingScopes.Sql, evt.Scope);
        Assert.Equal(LoggingCategories.DataSql, p[LoggingPropertyNames.Category]);
        Assert.Equal(LoggingOperations.Execute, p[LoggingPropertyNames.Operation]);
        Assert.Equal(LoggingSources.SqlServer, p[LoggingPropertyNames.Source]);
        Assert.Equal(Math.Round(expectedMs, 1), p[LoggingPropertyNames.ElapsedMs]);
        Assert.Equal(1, evt.Properties.Count(kv => kv.Key == LoggingPropertyNames.CommandType));
        Assert.Equal(1, evt.Properties.Count(kv => kv.Key == LoggingPropertyNames.ProcedureName));
        Assert.Equal(1, evt.Properties.Count(kv => kv.Key == LoggingPropertyNames.CommandName));
        var forbiddenKeys = evt.Properties.Keys.Where(k =>
            k.Contains("param", StringComparison.OrdinalIgnoreCase)
            || k.Contains("parameter", StringComparison.OrdinalIgnoreCase)
            || k.Contains("value", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Empty(forbiddenKeys);
    }

    // =========================================================================
    // INTERCEPTOR — emisión correcta
    // =========================================================================

    [Fact]
    public void Interceptor_ExceedThreshold_EmitsWithCanonicalContract()
    {
        var logger = CaptureLogger(out var emitted);
        var interceptor = CreateInterceptor(logger, Threshold(250));

        interceptor.EmitIfSlow(
            CreateCommand("SELECT TOP 10 NomUsuario FROM Usuarios"),
            TimeSpan.FromMilliseconds(512), CommandSource.LinqQuery);

        var evt = Assert.Single(emitted);
        AssertCanonicalContract(evt, 512);
        Assert.Equal("linqQuery", evt.Properties[LoggingPropertyNames.CommandType]);
        Assert.Equal("SELECT", evt.Properties[LoggingPropertyNames.CommandName]);
    }

    [Fact]
    public void Interceptor_SaveChanges_SourceMapsToSaveChanges()
    {
        var logger = CaptureLogger(out var emitted);
        var interceptor = CreateInterceptor(logger, Threshold(100));

        interceptor.EmitIfSlow(
            CreateCommand("INSERT INTO Usuarios (NomUsuario) VALUES (@p0)"),
            TimeSpan.FromMilliseconds(222), CommandSource.SaveChanges);

        var evt = Assert.Single(emitted);
        Assert.Equal("saveChanges", evt.Properties[LoggingPropertyNames.CommandType]);
        Assert.Equal("INSERT", evt.Properties[LoggingPropertyNames.CommandName]);
        Assert.DoesNotContain("@p0", string.Join(",", evt.Properties.Values.Select(v => v?.ToString())));
    }

    [Fact]
    public void Interceptor_ExecuteSqlRaw_SourceMapsToExecuteSqlRaw()
    {
        var logger = CaptureLogger(out var emitted);
        var interceptor = CreateInterceptor(logger, Threshold(100));

        interceptor.EmitIfSlow(
            CreateCommand("EXEC SP_Pwd_Cambiar @IdUsuario, @NuevaPwd"),
            TimeSpan.FromMilliseconds(333), CommandSource.ExecuteSqlRaw);

        var evt = Assert.Single(emitted);
        Assert.Equal("executeSqlRaw", evt.Properties[LoggingPropertyNames.CommandType]);
        Assert.Equal("SP_Pwd_Cambiar", evt.Properties[LoggingPropertyNames.ProcedureName]);
        Assert.Equal("EXEC", evt.Properties[LoggingPropertyNames.CommandName]);
    }

    [Fact]
    public void Interceptor_FromSqlQuery_SourceMapsToFromSqlQuery()
    {
        var logger = CaptureLogger(out var emitted);
        var interceptor = CreateInterceptor(logger, Threshold(100));

        interceptor.EmitIfSlow(
            CreateCommand("SELECT * FROM Sesiones WHERE IdTenant = {0}"),
            TimeSpan.FromMilliseconds(150), CommandSource.FromSqlQuery);

        var evt = Assert.Single(emitted);
        Assert.Equal("fromSqlQuery", evt.Properties[LoggingPropertyNames.CommandType]);
    }

    // =========================================================================
    // INTERCEPTOR — boundary del umbral
    // =========================================================================

    [Theory]
    [InlineData(249)]
    [InlineData(250)]
    [InlineData(251)]
    public void Interceptor_ThresholdBoundary_EmitsOnlyAtOrAbove(int elapsedMs)
    {
        var logger = CaptureLogger(out var emitted);
        var interceptor = CreateInterceptor(logger, Threshold(250));

        interceptor.EmitIfSlow(
            CreateCommand(FastText),
            TimeSpan.FromMilliseconds(elapsedMs), CommandSource.LinqQuery);

        if (elapsedMs < 250)
            Assert.Empty(emitted);
        else
            Assert.Single(emitted);
    }

    [Fact]
    public void Interceptor_ThresholdZeroOrNegative_UnconditionallyDisabled()
    {
        foreach (var threshold in new[] { 0, -100 })
        {
            var logger = CaptureLogger(out var emitted);
            var interceptor = CreateInterceptor(logger, Threshold(threshold));

            interceptor.EmitIfSlow(
                CreateCommand(FastText),
                TimeSpan.FromMilliseconds(50_000), CommandSource.LinqQuery);

            Assert.Empty(emitted);
        }
    }

    // =========================================================================
    // RAWQUERY (Async + Sync) — parity
    // =========================================================================

    [Fact]
    public void RawQuery_Async_SporThreshold_Emits_StoredProcedure()
    {
        var logger = CaptureLogger(out var emitted);
        var repo = CreateRawQueryAsync(logger, Threshold(250));

        repo.EmitIfSlow(FastSp, CommandType.StoredProcedure, 600);

        var evt = Assert.Single(emitted);
        AssertCanonicalContract(evt, 600);
        Assert.Equal("storedProcedure", evt.Properties[LoggingPropertyNames.CommandType]);
        Assert.Equal(FastSp, evt.Properties[LoggingPropertyNames.ProcedureName]);
        Assert.Equal(FastSp, evt.Properties[LoggingPropertyNames.CommandName]);
    }

    [Fact]
    public void RawQuery_Sync_BelowThreshold_NoEmit()
    {
        var logger = CaptureLogger(out var emitted);
        var repo = CreateRawQuerySync(logger, Threshold(250));

        repo.EmitIfSlow(FastSp, CommandType.StoredProcedure, 10);

        Assert.Empty(emitted);
    }

    [Fact]
    public void RawQuery_AsyncAndSync_ProduceIdenticalEvents_Parity()
    {
        var loggerAsync = CaptureLogger(out var logAsync);
        var loggerSync = CaptureLogger(out var logSync);
        var asyncRepo = CreateRawQueryAsync(loggerAsync, Threshold(250));
        var syncRepo = CreateRawQuerySync(loggerSync, Threshold(250));

        asyncRepo.EmitIfSlow("SELECT NomUsuario FROM Usuarios", CommandType.Text, 400);
        syncRepo.EmitIfSlow("SELECT NomUsuario FROM Usuarios", CommandType.Text, 400);

        var evtAsync = Assert.Single(logAsync);
        var evtSync = Assert.Single(logSync);

        Assert.Equal(evtAsync.EventName, evtSync.EventName);
        Assert.Equal(evtAsync.Scope, evtSync.Scope);
        Assert.Equal(evtAsync.Properties[LoggingPropertyNames.Category], evtSync.Properties[LoggingPropertyNames.Category]);
        Assert.Equal(evtAsync.Properties[LoggingPropertyNames.Operation], evtSync.Properties[LoggingPropertyNames.Operation]);
        Assert.Equal(evtAsync.Properties[LoggingPropertyNames.Source], evtSync.Properties[LoggingPropertyNames.Source]);
        Assert.Equal(evtAsync.Properties[LoggingPropertyNames.ElapsedMs], evtSync.Properties[LoggingPropertyNames.ElapsedMs]);
        Assert.Equal(evtAsync.Properties[LoggingPropertyNames.CommandType], evtSync.Properties[LoggingPropertyNames.CommandType]);
        Assert.Equal(evtAsync.Properties[LoggingPropertyNames.ProcedureName], evtSync.Properties[LoggingPropertyNames.ProcedureName]);
        Assert.Equal(evtAsync.Properties[LoggingPropertyNames.CommandName], evtSync.Properties[LoggingPropertyNames.CommandName]);
    }

    // =========================================================================
    // SANITIZADO — nunca parámetros ni valores
    // =========================================================================

    [Fact]
    public void SanitizeNames_StoredProcedure_KeepsOnlyIdentifier()
    {
        var (procedureName, commandName) =
            RawQueryRepositoryAsync.SanitizeNames(FastSp, CommandType.StoredProcedure);

        Assert.Equal(FastSp, procedureName);
        Assert.Equal(FastSp, commandName);
    }

    [Fact]
    public void SanitizeNames_Text_CommandNameIsFirstTokenOnly()
    {
        var (procedureName, commandName) =
            RawQueryRepositoryAsync.SanitizeNames(
                "SELECT Id, NomUsuario, PasswordHash FROM Usuarios WHERE Id = @p0", CommandType.Text);

        Assert.Equal("SELECT", commandName);
        Assert.Equal("SELECT", procedureName);
    }

    [Fact]
    public void SanitizeNames_EmptyText_ReturnsEmpty()
    {
        var (procedureName, commandName) =
            RawQueryRepositoryAsync.SanitizeNames("   ", CommandType.Text);

        Assert.Equal(string.Empty, procedureName);
        Assert.Equal(string.Empty, commandName);
    }

    [Theory]
    [InlineData("EXEC dbo.SP_Pwd_Cambiar @Id=1", "dbo.SP_Pwd_Cambiar")]
    [InlineData("exec [SP_Sesiones_Crear]", "[SP_Sesiones_Crear]")]
    public void Interceptor_SanitizeNames_ExexecExtractsProcedure(string text, string expectedProc)
    {
        var (procedureName, _) = SqlSlowQueryInterceptor.SanitizeNames(text);
        Assert.Equal(expectedProc, procedureName);
    }

    [Fact]
    public void Interceptor_CommandText_DoesNotLeakParameterValues()
    {
        var logger = CaptureLogger(out var emitted);
        var interceptor = CreateInterceptor(logger, Threshold(100));

        interceptor.EmitIfSlow(
            CreateCommand("SELECT * FROM Usuarios WHERE Email = N'juan@correo.com' AND IdTenant = @tp0"),
            TimeSpan.FromMilliseconds(500), CommandSource.LinqQuery);

        var evt = Assert.Single(emitted);
        var joined = string.Join("|", evt.Properties.Values.Select(v => v?.ToString()));
        Assert.DoesNotContain("juan@correo.com", joined);
        Assert.DoesNotContain("@tp0", joined);
    }

    // =========================================================================
    // CLIENTS — record / singleton mapping legacy
    // =========================================================================

    [Fact]
    public void MapCommandType_CoversStoredProcedureTextTableDirect()
    {
        Assert.Equal("storedProcedure", RawQueryRepositoryAsync.MapCommandType(CommandType.StoredProcedure));
        Assert.Equal("text", RawQueryRepositoryAsync.MapCommandType(CommandType.Text));
        Assert.Equal("tableDirect", RawQueryRepositoryAsync.MapCommandType(CommandType.TableDirect));
    }

    [Fact]
    public void MapCommandSource_CoversAllEfSources()
    {
        Assert.Equal("linqQuery", SqlSlowQueryInterceptor.MapCommandSource(CommandSource.LinqQuery));
        Assert.Equal("saveChanges", SqlSlowQueryInterceptor.MapCommandSource(CommandSource.SaveChanges));
        Assert.Equal("executeSqlRaw", SqlSlowQueryInterceptor.MapCommandSource(CommandSource.ExecuteSqlRaw));
        Assert.Equal("fromSqlQuery", SqlSlowQueryInterceptor.MapCommandSource(CommandSource.FromSqlQuery));
        Assert.Equal("text", SqlSlowQueryInterceptor.MapCommandSource((CommandSource)999));
    }
}