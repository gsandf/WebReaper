﻿using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using WebReaper.Abstractions.JobQueue;
using WebReaper.Domain;

namespace WebReaper.Queue.AzureServiceBus;

public class AzureJobQueueWriter : IJobQueueWriter, IAsyncDisposable
{
    private ServiceBusClient client;
    private ServiceBusSender sender;

    public AzureJobQueueWriter(string serviceBusConnectionString, string queueName)
    {
        // create a Service Bus client
        client = new(serviceBusConnectionString);
        // create a sender for the queue 
        sender = client.CreateSender(queueName);
    }

    public async Task WriteAsync(Job job)
    {
        var json = JsonConvert.SerializeObject(job);
        await sender.SendMessageAsync(new ServiceBusMessage(json));
    }

    public async Task CompleteAddingAsync()
    {
        await sender.CloseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
    }
}