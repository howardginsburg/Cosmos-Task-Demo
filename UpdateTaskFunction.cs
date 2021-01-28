using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Dynamic;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;

namespace Demo.Task
{
    /**
        UpdateTask function.
    */
    public class UpdateTaskFunction
    {
        private CosmosClient _cosmosClient;
        private Container _taskContainer;
        public UpdateTaskFunction(CosmosClient cosmosClient)
        {
             //Get the cosmos client object that our Startup.cs creates through dependency injection.
            _cosmosClient = cosmosClient;

            //Get the container we need to query.
            _taskContainer = _cosmosClient.GetContainer("Tasks","TaskItem");
        }


         /**
            UpdateTask function.
        */
        [FunctionName("UpdateTask")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]
            HttpRequest req,
            ILogger log)
        {
            //Get the json from the payload and turn it into a dynamic object so we don't need a model class.
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic task = JsonConvert.DeserializeObject<ExpandoObject>(requestBody);

            //If this is a new task, it will not have an id.  If we cast the ExpandoObject as a Dictionary, we can do a lookup to see if the attribute exists.
            //Create the guid here so we can easily return it in the response.
            if (((IDictionary<String, object>)task).ContainsKey("id") == false) 
            {
                task.id = Guid.NewGuid().ToString();
            }

            //If the task is complete, use ttl to let it get deleted.  This way the change feed will pickup the document update and alter the managed views.
            if (task.status.Equals("complete"))
            {
                //Set the ttl to 5 minutes. (60 seconds * 5 minutes)
                task.ttl = 60 * 5;
            }

            //Upsert the document in cosmos and return the task id.
            var result = await _taskContainer.UpsertItemAsync<Object>(item: task, partitionKey: new PartitionKey(task.id));
            log.LogInformation($"Upserted task {task.id} with RU charge {result.RequestCharge}");
            return new OkObjectResult(task.id);
        }
    }
}
