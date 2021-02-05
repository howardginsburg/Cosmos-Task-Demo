using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Documents;

namespace Demo.Task
{

    /**
        GetTask function.
    */
    public static class GetTaskFunction
    {
        [FunctionName("GetTask")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetTask/{id}")] 
            HttpRequest req,
            //CosmosNote - For V2 SDK, we can use function bindings to retrieve the document thus making the code in the function very simple.
            [CosmosDB("Tasks", 
                      "TaskItem", 
                      ConnectionStringSetting="CosmosDbConnection", 
                      Id="{id}",
                      PartitionKey="{id}")]
            Document document,
            ILogger log)
        {
            //The binding does the lookup for us, minimizing the code.
            if(document == null) return new NotFoundResult();
            return new JsonResult(document);
        }
    }
}
