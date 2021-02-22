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

namespace Demo.Task
{
    /**
        UpdateTask function.
    */
    public static class UpdateTaskFunction
    {
        /**
            UpdateTask function.
        */
        [FunctionName("UpdateTask")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = null)]
            HttpRequest req,
            //CosmosNote - we can use a document output binding to handle the write back to cosmos, thus minimizing the cosmos specific code.
            [CosmosDB(
                databaseName: "Tasks",
                collectionName: "TaskItem",
                ConnectionStringSetting = "CosmosDBConnection")]out Document document,
            ILogger log)
        {
            //Get the json from the payload and turn it into an ExpandoObject so we don't need a model class.  We could convert this directly to the Cosmos Document class,
            //but we're trying to have the code bases as close to the same as possible.
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic task = JsonConvert.DeserializeObject<ExpandoObject>(requestBody);

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

            //CosmosNote - Convert from our ExpandoObject to the Document so the binding picks it up and saves it.  As noted above, we could 
            //have just deserialized directly to the Document and used the Get/SetProperty methods so we didn't have to convert.
            document = JsonConvert.DeserializeObject<Document>(JsonConvert.SerializeObject(task));
            
            //Return the task id.  The document will get upserted for us when the function returns.
            return new OkObjectResult(task.id);
        }
    }
}
