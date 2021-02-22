using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Demo.TaskDemo
{

    /**
        GetTask function.
        
    */
    public class GetTaskFunction
    {
        private CosmosHelper _cosmosHelper;
        
        //CosmosNote - In order to easily get the CosmosHelper to read and update Tasks, we use dependency injection.  This is different from V2 where we can use
        //the Cosmos function binding to get the DocumentClient, and then build the CosmosHelper.
        public GetTaskFunction(CosmosHelper cosmosHelper)
        {
            //Get the cosmos client object that our Startup.cs creates through dependency injection.
            _cosmosHelper = cosmosHelper;
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
            //CosmosNote - we must retieve the document through code.  This is fairly simple, but not as elegant as the function binding in V2.
            //Use dynamic so we don't need to have a model object.
            dynamic task = await _cosmosHelper.ReadItemAsync("Tasks","TaskItem",id, log);
            //The binding does the lookup for us, minimizing the code.
            if(task == null) return new NotFoundResult();
            return new JsonResult(task);
        }
    }
}
