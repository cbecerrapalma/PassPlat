using CBP.Events;
using CBP.Events.DependencyInjection;
using CBP.Logging;
using CBP.Logging.Interfaces;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Aplicacion.Test.Tests.Framework.S17;

namespace PassPlat.Aplicacion.Test.Tests.Framework.S22;

public record S22ContractTestEvent(string Payload) : EventBase
{
    public override string EventType => "S22.ContractTest";
}

public class S22ContractHandler : IEventHandler<S22ContractTestEvent>
{
    public Task<Result> HandleAsync(S22ContractTestEvent @event, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}

/// <summary>
/// S22 — Contract tests post-refactor. Verifica que la renombrada API de
/// CBP.Events (AddCBPEvents / IEventDispatcher / EventDispatcher / ICBPEvent)
/// mantiene el contrato funcional S21:S16.4: resolución DI scoped,
/// publish → dispatch → handler y propagación de CorrelationId.
/// </summary>
public class EventContractTests
{
    [Fact]
    public void T1_AddCBPEvents_Registers_Scoped_Dispatcher_And_Publisher()
    {
        var services = new ServiceCollection();
        services.AddCBPEvents();

        var provider = services.BuildServiceProvider();

        using (provider.CreateScope())
        {
            Assert.NotNull(provider.GetService<IEventDispatcher>());
        }

        using (provider.CreateScope())
        {
            Assert.NotNull(provider.GetService<IEventPublisher>());
        }

        using (var scope1 = provider.CreateScope())
        using (var scope2 = provider.CreateScope())
        {
            var dispatcher1 = scope1.ServiceProvider.GetRequiredService<IEventDispatcher>();
            var dispatcher2 = scope2.ServiceProvider.GetRequiredService<IEventDispatcher>();

            Assert.NotNull(dispatcher1);
            Assert.NotSame(dispatcher1, dispatcher2);
        }
    }

    [Fact]
    public async Task T2_Publish_Dispatches_Handler_With_CorrelationId()
    {
        var capture = new CapturingLoggerService();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerService>(capture);
        services.AddCBPEvents();
        services.AddEventHandler<S22ContractTestEvent, S22ContractHandler>();

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var @event = new S22ContractTestEvent("payload")
                .WithCorrelationId("s22-contract-correlation-id");

            var result = await publisher.PublishAsync(@event);

            Assert.True(result.IsSuccess, $"Error: {result.Error?.Code} | {result.Error?.Message}");
        }

        var published = capture.Events(LoggingEvents.EventPublished);
        Assert.Single(published);
        Assert.Equal(LoggingScopes.DomainEvents, published[0].Scope);
        Assert.Equal("s22-contract-correlation-id",
            published[0].Properties[LoggingPropertyNames.CorrelationId]);

        var handled = capture.Events(LoggingEvents.EventHandled);
        Assert.Single(handled);
        Assert.Empty(capture.Events(LoggingEvents.EventFailed));
    }
}