using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Demo.Task
{
    public static class GetTask
    {
        [FunctionName("GetTask")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "task/{taskid}")] 
            HttpRequest req, 
            [CosmosDB("Tasks", 
                      "TaskItem", 
                      ConnectionStringSetting="CosmosDbConnection", 
                      Id="{taskid}",
                      PartitionKey="{taskid}")]
            Document taskDocument, 
            ILogger log)
        {
            if(taskDocument == null) return new NotFoundResult();
            return new JsonResult(taskDocument);
        }
    }
}
