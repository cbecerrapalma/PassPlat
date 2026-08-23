using CBP.Events;
using CBP.Logging;
using CBP.Logging.Interfaces;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;

namespace PassPlat.Aplicacion.Test.Tests.Framework.S17;

public record S17TestEvent(string Payload) : EventBase
{
    public override string EventType => "S17.Test";
}

public class S17SuccessHandler : IEventHandler<S17TestEvent>
{
    public Task<Result> HandleAsync(S17TestEvent @event, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}

public class S17FailingHandler : IEventHandler<S17TestEvent>
{
    public Task<Result> HandleAsync(S17TestEvent @event, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Failure("S17_HANDLER_FAILED", "handler fail"));
}

/// <summary>
/// S17 — T4/T5/T6. Instrumentación Event_Published / Event_Handled / Event_Failed
/// en EventDispatcher (modo DI y modo manual).
/// </summary>
public class EventDispatcherInstrumentationTests
{
    private static EventDispatcher CreateDiDispatcher(
        CapturingLoggerService capture, params object[] handlers)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerService>(capture);
        foreach (var handler in handlers)
        {
            var handlerInterface = handler.GetType().GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));
            services.AddScoped(handlerInterface, _ => handler);
        }
        var provider = services.BuildServiceProvider();
        return new EventDispatcher(provider);
    }

    [Fact]
    public async Task T4_DiDispatch_Success_Emits_Published_And_Handled_Per_Handler()
    {
        var capture = new CapturingLoggerService();
        var dispatcher = CreateDiDispatcher(capture, new S17SuccessHandler(), new S17SuccessHandler());

        var @event = new S17TestEvent("payload");
        var result = await dispatcher.DispatchAsync(@event);

        Assert.True(result.IsSuccess, $"Error: {result.Error?.Code} | {result.Error?.Message} | details={string.Join(';', result.Error?.Details?.Select(d => $"{d.Key}={d.Value}") ?? Enumerable.Empty<string>())}");
        Assert.Equal(2, capture.Events(LoggingEvents.EventHandled).Count);
        var published = capture.Events(LoggingEvents.EventPublished);
        Assert.Single(published);
        Assert.Equal(LoggingScopes.DomainEvents, published[0].Scope);
        Assert.Equal(LoggingOperations.Publish,
            published[0].Properties[LoggingPropertyNames.Operation]);
        Assert.Equal(@event.EventType,
            published[0].Properties[LoggingPropertyNames.Event]);
        Assert.Empty(capture.Events(LoggingEvents.EventFailed));
    }

    [Fact]
    public async Task T5_Handler_Failure_Emits_Event_Failed_And_Conserves_Result_Failure()
    {
        var capture = new CapturingLoggerService();
        var dispatcher = CreateDiDispatcher(capture, new S17FailingHandler());

        var result = await dispatcher.DispatchAsync(new S17TestEvent("payload"));

        Assert.False(result.IsSuccess);
        Assert.Equal("EVENT_HANDLING_FAILED", result.Error?.Code);
        var failed = capture.Events(LoggingEvents.EventFailed);
        Assert.Single(failed);
        Assert.Equal(LoggingScopes.DomainEvents, failed[0].Scope);
        Assert.Equal(LoggingOperations.Handle,
            failed[0].Properties[LoggingPropertyNames.Operation]);
        Assert.Equal("S17FailingHandler",
            failed[0].Properties[LoggingPropertyNames.Method]);
    }

    [Fact]
    public async Task T6_ManualMode_Without_ILoggerService_Is_NoOp_No_Throw()
    {
        var handlers = new Dictionary<Type, List<object>>
        {
            [typeof(IEventHandler<S17TestEvent>)] = new() { new S17SuccessHandler() }
        };
        var dispatcher = new EventDispatcher(handlers);

        var result = await dispatcher.DispatchAsync(new S17TestEvent("payload"));

        var all = string.Join(';', result.Error?.Details?.Select(d => $"{d.Key}={d.Value}") ?? Enumerable.Empty<string>());
        Assert.True(result.IsSuccess, $"Error: {result.Error?.Code} | {result.Error?.Message} | details={all}");
    }
}
