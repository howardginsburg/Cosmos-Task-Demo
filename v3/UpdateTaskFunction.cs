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

namespace Demo.TaskDemo
{
    /**
        UpdateTask function.
    */
    public class UpdateTaskFunction
    {
        private CosmosHelper _cosmosHelper;
        
        //CosmosNote - In order to easily get the CosmosHelper to read and update Tasks, we use dependency injection.  This is different from V2 where we can use
        //the Cosmos function binding to get the DocumentClient, and then build the CosmosHelper. 
        public UpdateTaskFunction(CosmosHelper cosmosHelper)
        {
            //Get the cosmos client object that our Startup.cs creates through dependency injection.
            _cosmosHelper = cosmosHelper;
        }

         /**
            UpdateTask function.
        */
        [FunctionName("UpdateTask")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = null)]
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

                //In theory, we'd want the function to set the created date for new records.  However, the client data generator may set it for us
                //to simulate date ranges.
                if (((IDictionary<String, object>)task).ContainsKey("createddate") == false)
                {
                    task.createddate = DateTime.UtcNow.ToString("o");
                }
            }

            //If the task is complete, use ttl to let it get deleted.  This way the change feed will pickup the document update and alter the managed views.
            if (task.status.Equals("complete"))
            {
                //Set the ttl to 5 minutes. (60 seconds * 5 minutes)
                task.ttl = 60 * 5;

                //In theory, we'd want the function to set the completed date for new records.  However, the client data generator may set it for us
                //to simulate date ranges.
                if (((IDictionary<String, object>)task).ContainsKey("completeddate") == false)
                {
                    task.completeddate = DateTime.UtcNow.ToString("o");
                }
            }

            //CosmosNote - Upsert the document in cosmos and return the task id.  This is different from V2 where we use a Document output binding to handle it for us.
            await _cosmosHelper.UpsertItemAsync("Tasks","TaskItem",task,false,log);
            return new OkObjectResult(task.id);
        }
    }
}
