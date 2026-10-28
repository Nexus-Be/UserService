namespace User.Application.Consumers;

using MassTransit;

public static class ConsumerExtensions
{
    public static IBusRegistrationConfigurator AddApplicationConsumers(this IBusRegistrationConfigurator configurator)
    {
        // Register User service consumers here when inbound events are added
        return configurator;
    }
}
