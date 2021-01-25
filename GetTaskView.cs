using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Demo.Task
{
    public static class GetTaskView
    {
        [FunctionName("GetTaskView")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "user/{userid}")] 
            HttpRequest req, 
            [CosmosDB("Tasks", 
                      "TaskViews", 
                      ConnectionStringSetting="CosmosDbConnection", 
                      Id="{userid}",
                      PartitionKey="{userid}")]
            Document taskDocument, 
            ILogger log)
        {
            if(taskDocument == null) return new NotFoundResult();
            return new JsonResult(taskDocument);
        }
    }
}
