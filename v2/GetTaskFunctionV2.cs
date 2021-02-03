using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Documents;

namespace Demo.Task.V2
{

    /**
        GetTask function.
    */
    public static class GetTaskFunctionV2
    {
        [FunctionName("GetTaskFunctionV2")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetTaskV2/{id}")] 
            HttpRequest req,
            [CosmosDB("Tasks", 
                      "TaskItem", 
                      ConnectionStringSetting="CosmosDbConnection", 
                      Id="{id}",
                      PartitionKey="{id}")]
            Document customerDocument,
            ILogger log)
        {
            //The binding does the lookup for us, minimizing the code.
            if(customerDocument == null) return new NotFoundResult();
            return new JsonResult(customerDocument);
        }
    }
}
