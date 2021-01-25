using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Demo.Task
{
    public static class UpdateTask
    {
        [FunctionName("UpdateTask")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]
            HttpRequest req,
            [CosmosDB("Tasks",
                      "TaskItem",
                      ConnectionStringSetting = "CosmosDBConnection")]
            IAsyncCollector<JObject> tasks,
            ILogger log)
        {
            //log.LogInformation("C# HTTP trigger function processed a request.");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic task = JsonConvert.DeserializeObject<JObject>(requestBody);
            await tasks.AddAsync(task);
            return new OkResult();
        }
    }
}
