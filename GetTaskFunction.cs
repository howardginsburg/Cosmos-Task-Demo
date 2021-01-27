using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Demo.Task
{

    /**
        GetTask function.
    */
    public class GetTaskFunction
    {
        
        private CosmosClient _cosmosClient;
        private Container _taskContainer;
        public GetTaskFunction(CosmosClient cosmosClient)
        {
            //Get the cosmos client object that our Startup.cs creates through dependency injection.
            _cosmosClient = cosmosClient;

            //Get the container we need to query.
            _taskContainer = _cosmosClient.GetContainer("Tasks","TaskItem");
        }

        /**
            Function to retrieve a task by task id.
        */
        [FunctionName("GetTask")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetTask/{id}")] 
            HttpRequest req,  
            string id,
            ILogger log)
        {
            //Use dynamic so we don't need to have a model object.
            dynamic task = null;
            try
            {
                //Query for the document an Object so we don't need to have a model class defined.
                ItemResponse<Object> response = await _taskContainer.ReadItemAsync<Object>(id: id, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(id));
                log.LogInformation($"Retrieved task {id} with RU charge {response.RequestCharge}");

                //Convert the document to json, which is located in the Resource parameter and needs to be a string
                task = JsonConvert.DeserializeObject<Object>(response.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                //Handle document not found.
                if (ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                {
                    return new NotFoundResult();
                }
                else{
                    throw;
                }

            }
            return new JsonResult(task);

        }
    }
}
