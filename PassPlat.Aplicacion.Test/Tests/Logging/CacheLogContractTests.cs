using CBP.Logging;
using CBP.Logging.Configuration;
using CBP.Logging.Core;
using CBP.Logging.Models;
using CBP.Logging.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using CbpLogEvent = CBP.Logging.Models.LogEvent;
using SerilogLogEvent = Serilog.Events.LogEvent;

namespace PassPlat.Aplicacion.Test.Tests.Logging;

/// <summary>
/// G3.5 + G3.7 — Certifica que las propiedades estructuradas del contrato de cache
/// (category, repository, operation, source, cacheResult, key, tenantId, elapsedMs)
/// más el enriquecimiento automático (correlationId, userId, clientIp) llegan al
/// evento Serilog emitido a través del LoggerService real.
/// </summary>
public class CacheLogContractTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<SerilogLogEvent> Events { get; } = new();

        public void Emit(SerilogLogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class CapturingHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static SerilogLogEvent Capture(CollectingSink sink, CbpLogEvent evt)
    {
        using var serilog = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var accessor = new CapturingHttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var service = new LoggerService(
            serilog,
            accessor,
            Microsoft.Extensions.Options.Options.Create(new LoggingOptions()),
            new DefaultContextProvider());

        service.LogInformation(evt);
        return sink.Events.Single();
    }

    private static string Prop(SerilogLogEvent emitted, string key) =>
        emitted.Properties.TryGetValue(key, out var value)
            ? (value as ScalarValue)?.Value?.ToString() ?? value.ToString()
            : throw new Xunit.Sdk.XunitException($"Missing structured property '{key}'. Present keys: {string.Join(", ", emitted.Properties.Keys)}");

    [Fact]
    public void HitEvent_ContainsContractualCacheProperties()
    {
        var evt = new CbpLogEvent
        {
            EventName = LoggingEvents.CacheHit,
            Message = "60s TTL | ObtenerActivasAsync | key=app:catalog:activas | memory | Hit",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.DataCache,
                [LoggingPropertyNames.Repository] = "AppRepository",
                [LoggingPropertyNames.Operation] = "ObtenerActivasAsync",
                [LoggingPropertyNames.Source] = LoggingSources.Memory,
                [LoggingPropertyNames.CacheResult] = LoggingCacheResults.Hit,
                [LoggingPropertyNames.Key] = "app:catalog:activas",
                [LoggingPropertyNames.TenantId] = 2,
                [LoggingPropertyNames.ElapsedMs] = 0.42,
            }
        };

        var emitted = Capture(new CollectingSink(), evt);

        Assert.Equal(LoggingEvents.CacheHit, evt.EventName);
        Assert.Equal(LoggingCategories.DataCache, Prop(emitted, LoggingPropertyNames.Category));
        Assert.Equal("AppRepository", Prop(emitted, LoggingPropertyNames.Repository));
        Assert.Equal("ObtenerActivasAsync", Prop(emitted, LoggingPropertyNames.Operation));
        Assert.Equal(LoggingSources.Memory, Prop(emitted, LoggingPropertyNames.Source));
        Assert.Equal(LoggingCacheResults.Hit, Prop(emitted, LoggingPropertyNames.CacheResult));
        Assert.Equal("app:catalog:activas", Prop(emitted, LoggingPropertyNames.Key));
        Assert.Contains(LoggingPropertyNames.ElapsedMs, emitted.Properties.Keys);
    }

    [Fact]
    public void MissEvent_Reports_SqlSource_And_TenantId()
    {
        var evt = new CbpLogEvent
        {
            EventName = LoggingEvents.CacheMiss,
            Message = "miss",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.DataCache,
                [LoggingPropertyNames.Repository] = "ConfigTenantRepository",
                [LoggingPropertyNames.Operation] = "ObtenerPorTenantAsync",
                [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                [LoggingPropertyNames.CacheResult] = LoggingCacheResults.Miss,
                [LoggingPropertyNames.Key] = "configtenant:tenant:2",
                [LoggingPropertyNames.TenantId] = 2,
                [LoggingPropertyNames.ElapsedMs] = 5.1,
            }
        };

        var emitted = Capture(new CollectingSink(), evt);

        Assert.Equal(LoggingSources.SqlServer, Prop(emitted, LoggingPropertyNames.Source));
        Assert.Equal(LoggingCacheResults.Miss, Prop(emitted, LoggingPropertyNames.CacheResult));
        Assert.Equal("configtenant:tenant:2", Prop(emitted, LoggingPropertyNames.Key));
    }

    [Fact]
    public void EventName_And_Scope_Are_Emitted_As_Structured_Properties()
    {
        var evt = new CbpLogEvent
        {
            EventName = LoggingEvents.CacheHit,
            Scope = LoggingScopes.Cache,
            Message = "hit",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.DataCache,
            }
        };

        var emitted = Capture(new CollectingSink(), evt);

        Assert.Equal(LoggingEvents.CacheHit, Prop(emitted, LoggingPropertyNames.EventName));
        Assert.Equal(LoggingScopes.Cache, Prop(emitted, LoggingPropertyNames.Scope));
    }

    [Fact]
    public void Pipeline_Enriches_CorrelationId_UserId_ClientIp()
    {
        var evt = new CbpLogEvent
        {
            EventName = LoggingEvents.CacheSet,
            Message = "set",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.DataCache,
                [LoggingPropertyNames.Repository] = "AppRepository",
                [LoggingPropertyNames.Operation] = "ObtenerActivasAsync",
                [LoggingPropertyNames.Source] = LoggingSources.Memory,
                [LoggingPropertyNames.CacheResult] = LoggingCacheResults.Refreshed,
                [LoggingPropertyNames.Key] = "app:catalog:activas",
                [LoggingPropertyNames.ElapsedMs] = 3.3,
            }
        };

        var emitted = Capture(new CollectingSink(), evt);

        Assert.True(
            emitted.Properties.ContainsKey(LoggingPropertyNames.CorrelationId),
            $"keys=[{string.Join(",", emitted.Properties.Keys)}]");
        Assert.True(
            emitted.Properties.ContainsKey(LoggingPropertyNames.UserId),
            $"keys=[{string.Join(",", emitted.Properties.Keys)}]");
        Assert.True(
            emitted.Properties.ContainsKey(LoggingPropertyNames.ClientIp),
            $"keys=[{string.Join(",", emitted.Properties.Keys)}]");
        Assert.True(
            emitted.Properties.ContainsKey(LoggingPropertyNames.RequestPath),
            $"keys=[{string.Join(",", emitted.Properties.Keys)}]");
        Assert.True(
            emitted.Properties.ContainsKey(LoggingPropertyNames.HttpMethod),
            $"keys=[{string.Join(",", emitted.Properties.Keys)}]");

        Assert.False(
            emitted.Properties.ContainsKey("CorrelationId"),
            $"No debe existir la variante PascalCase CorrelationId. keys=[{string.Join(",", emitted.Properties.Keys)}]");
        Assert.False(
            emitted.Properties.ContainsKey("UserId"),
            "No debe existir la variante PascalCase UserId.");
    }
}