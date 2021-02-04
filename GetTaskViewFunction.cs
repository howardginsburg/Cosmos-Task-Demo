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
        GetTaskView function.
    */
    public class GetTaskViewFunction
    {
        private CosmosClient _cosmosClient;
        private Container _taskViewContainer;

        //CosmosNote - In order to easily get the CosmosClient to read and update TaskViews, we use dependency injection.  This is different from V2 where we can use
        //the Cosmos function binding. 
        public GetTaskViewFunction(CosmosClient cosmosClient)
        {
            //Get the cosmos client object that our Startup.cs creates through dependency injection.
            _cosmosClient = cosmosClient;

            //Get the container we need to query.
            _taskViewContainer = _cosmosClient.GetContainer("Tasks","TaskViews");
        }

        [FunctionName("GetTaskView")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetTaskView/{id}")] 
            HttpRequest req, 
            string id,
            ILogger log)
        {
            //CosmosNote - we must retieve the document through code.  This is fairly simple, but not as elegant as the function binding in V2.
            //Use dynamic so we don't need to have a model object.
            dynamic task = null;
            try
            {
                //Query for the document an Object so we don't need to have a model class defined.
                ItemResponse<Object> response = await _taskViewContainer.ReadItemAsync<Object>(id: id, partitionKey: new PartitionKey(id));
                log.LogInformation($"Retrieved task {id} with RU charge {response.RequestCharge}");

                //Convert the document to json, which is located in the Resource parameter and needs to be a string.
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
