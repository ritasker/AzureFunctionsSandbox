using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Pier8.EventGrid
{
    public static class CreateUser
    {
        [FunctionName("CreateUser")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users")] HttpRequest req, 
            [Queue("users"), StorageAccount("AzureWebJobsStorage")] IAsyncCollector<CreateUserMessage> createUserCollector,
            ILogger log)
        {
            var jsonSerializerOptions = new JsonSerializerOptions{ PropertyNameCaseInsensitive = true};
            var msg = await System.Text.Json.JsonSerializer.DeserializeAsync<CreateUserMessage>(req.Body, jsonSerializerOptions);
            await createUserCollector.AddAsync(msg);
            await createUserCollector.FlushAsync();
            return new AcceptedResult();
        }
    }
    
    public static class ConsumerA
    {
        [FunctionName(nameof(ConsumerA))]
        public static Task RunAsync([EventGridTrigger]EventGridEvent evt, ILogger log)
        {
            var jsonSerializerOptions = new JsonSerializerOptions{ PropertyNameCaseInsensitive = true};
            var createUserMessage = JsonSerializer.Deserialize<CreateUserMessage>(msg, jsonSerializerOptions);
            log.LogInformation("{FuncName} got a message. Username: {Username}", nameof(ConsumerA), createUserMessage.Username);
            return Task.CompletedTask;
        }
    }
    
    public static class ConsumerB
    {
        [FunctionName(nameof(ConsumerB))]
        public static Task RunAsync([EventGridTrigger]EventGridEvent evt, ILogger log)
        {
            var jsonSerializerOptions = new JsonSerializerOptions{ PropertyNameCaseInsensitive = true};
            var createUserMessage = JsonSerializer.Deserialize<CreateUserMessage>(evt.Data, jsonSerializerOptions);
            log.LogInformation("{FuncName} got a message. Email: {Email}", nameof(ConsumerB), createUserMessage.Email);
            return Task.CompletedTask;
        }
    }
}