using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Documents;
using System.Dynamic;
using System.Collections.Generic;

namespace Demo.Task.V2
{
    /**
        UpdateTask function.
    */
    public static class UpdateTaskFunctionV2
    {
        /**
            UpdateTask function.
        */
        [FunctionName("UpdateTaskV2")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]
            HttpRequest req,
            [CosmosDB(
                databaseName: "Tasks",
                collectionName: "TaskItem",
                ConnectionStringSetting = "CosmosDBConnection")]out Document document,
            ILogger log)
        {
            //Get the json from the payload and turn it into an ExpandoObject so we don't need a model class.  We could convert this directly to the Cosmos Document class,
            //but this tries to show the similarities 
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic task = JsonConvert.DeserializeObject<ExpandoObject>(requestBody);

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

            //Conert from our ExpandoObject to the Document so the binding picks it up and saves it.  As noted above, we could have just deserialized directly to the Document
            //and used the Get
            document = JsonConvert.DeserializeObject<Document>(JsonConvert.SerializeObject(task));
            
            //Upsert the document in cosmos and return the task id.
            return new OkObjectResult(task.id);
        }
    }
}
