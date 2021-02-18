using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
//Cosmos V2 SDK.
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Azure.Documents.Client;


namespace Demo.TaskDemo
{
    /**
        TaskUpdateChangeFeed function that manages the task view for approvers.  As tasks change, the Cosmos changefeed will fire.
    */
    public class TaskUpdateChangeFeedFunction
    { 
               
        /**
            TaskUpdateChangeFeed function.

            This function is a Cosmos ChangeFeed trigger
            CosmosNote - In order to use the ChangeFeed trigger, we must use V2 SDK. 
        */
        [FunctionName("TaskUpdateChangeFeed")]
        public async void Run([CosmosDBTrigger(
            databaseName: "Tasks",
            collectionName: "TaskItem",
            ConnectionStringSetting = "CosmosDBConnection",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)] IReadOnlyList<Document> input,
            //CosmosNote - For V2 SDK, we can use the cosmos binding to get a handle to the DocumentClient object.  This is different than
            //V3 SDK where we use dependency injection to get the handle and abstract it from the function.
            [CosmosDB(
                databaseName: "Tasks",
                collectionName: "TaskViews",
                ConnectionStringSetting = "CosmosDBConnection")] DocumentClient _cosmosClient,
            ExecutionContext context, 
            ILogger log)
        {
            //Loop through all the v2 Documents we receive in the change feed.				
            if (input != null && input.Count > 0)
            {
                log.LogInformation("Documents modified " + input.Count);

                foreach (Document document in input)
                {
                    //Convert the document into a dynamic object so we can work with it without binding to a model.
                    dynamic task = JsonConvert.DeserializeObject<ExpandoObject>(document.ToString(), new ExpandoObjectConverter());

                    //Get the TaskItemView for this submitter.
                    while (true)
                    {
                        try
                        {
                            dynamic taskOwnerView = await getTaskView(task.submittedby, _cosmosClient, log);
                            handleTaskApprovals(task, taskOwnerView, log);
                            await saveTaskView(taskOwnerView, _cosmosClient, log);
                            break;
                        }
                        catch (DocumentClientException ex)
                        {
                            //If the document etag did not match, retrieve again and try again.
                            if (!ex.StatusCode.Equals(System.Net.HttpStatusCode.PreconditionFailed))
                            {
                                throw ex;
                            }
                        }
                    }
                    
                    //Loop through all the approvers in the document.
                    foreach(dynamic approver in task.approvers)
                    {
                        while (true)
                        {
                            try
                            {    
                                //Get the TaskItemView for this approver.

                                dynamic taskApproverView = await getTaskView(approver.id, _cosmosClient, log);
                                handleTaskApprovals(task, taskApproverView, log);
                                await saveTaskView(taskApproverView, _cosmosClient, log);
                                break;
                            }
                            catch (DocumentClientException ex)
                            {
                                //If the document etag did not match, retrieve again and try again.
                                if (!ex.StatusCode.Equals(System.Net.HttpStatusCode.PreconditionFailed))
                                {
                                    throw ex;
                                }
                            }
                        }
                    }
                    
                }
            }

        }


        private async Task<dynamic> getTaskView(dynamic id, DocumentClient _cosmosClient, ILogger log)
        {
            dynamic taskView = null;
            try{
                //CosmosNote - Using the Comsos V2 SDK, read the TaskItemView as a Document type.  
                //This allows us to not have to have a model class before turning it into a dynamic object.
                RequestOptions options = new RequestOptions();
                options.PartitionKey = new PartitionKey(id);

                Uri uri = UriFactory.CreateDocumentUri("Tasks","TaskViews",id);
                Document document = await _cosmosClient.ReadDocumentAsync(uri, options);
                taskView = JsonConvert.DeserializeObject<ExpandoObject>(document.ToString(), new ExpandoObjectConverter());
            }
            catch (DocumentClientException ex)
            {
                //If we don't have a document in Cosmos for this user, a NotFound exception will be thrown and we can create an initial stub to use.
                if (ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                {
                    log.LogInformation($"User {id} does not have a TaskItemView.  A new document will be created.");
                    taskView = new ExpandoObject();
                    taskView.id = id;
                    taskView.mytasks = new List<ExpandoObject>();
                    taskView.approvaltasks = new List<ExpandoObject>();
                }
                else{
                    throw;
                }

            }
            return taskView;
        }

        private async System.Threading.Tasks.Task saveTaskView(dynamic taskView, DocumentClient _cosmosClient, ILogger log)
        {
            RequestOptions options = new RequestOptions();
            options.PartitionKey = new PartitionKey(taskView.id);

            //If this is an existing document, it will have an etag.  Lets make sure we don't overwrite the view if
            //it was updated by a parallel function running.                
            if (((IDictionary<String, object>)taskView).ContainsKey("_etag"))
            {
                AccessCondition accessCondition = new AccessCondition();
                accessCondition.Condition = taskView._etag;
                accessCondition.Type = AccessConditionType.IfMatch;
                options.AccessCondition = accessCondition;
            }
            
            
            //If they still have tasks to approve, go ahead and upsert the document.  Otherwise, delete it for good housekeeping.
            if ((taskView.mytasks.Count > 0) || (taskView.approvaltasks.Count > 0))
            {
                //CosmosNote - to upsert a document in V2, we must build the URI for the collection.  V3 handles this differently requiring the document and the partition.
                Uri uri = UriFactory.CreateDocumentCollectionUri("Tasks","TaskViews");
                var result = await _cosmosClient.UpsertDocumentAsync(uri, taskView,options);
                log.LogInformation($"Upserted TaskItemView for  {taskView.id} with RU charge {result.RequestCharge}");
            }
            else
            {
                //CosmosNote - to delete a document in V2, we need the URI of the document.  V3 handles this differently requiring the document id and partition key.
                try{
                    
                    Uri uri = UriFactory.CreateDocumentUri("Tasks","TaskViews",taskView.id);
                    var result = await _cosmosClient.DeleteDocumentAsync(uri,options);
                    log.LogInformation($"Deleted TaskItemView document for  {taskView.id} as there are no remaining approvals with RU charge {result.RequestCharge}");
                }
                catch (DocumentClientException ex)
                {
                    //If we don't have a document in Cosmos for this user, a NotFound exception will be thrown and we can create an initial stub to use.
                    if (!ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                    {
                        throw ex;
                    }
                }
            }
        }

        private void handleTaskApprovals(dynamic task, dynamic taskView, ILogger log)
        {
            if (task.status.Equals("pending"))
            {
                //Create a new dynamic base task for approval.
                dynamic taskElement = new ExpandoObject();
                taskElement.id = task.id;
                taskElement.type = task.type;
                taskElement.summary = task.summary;

                //Determine if this is a task submitted by the user, or an approval that needs to be added and put it in the right
                //area of the view.
                dynamic taskList = null;
                if (task.submittedby.Equals(taskView.id))
                {
                    taskList = taskView.mytasks;
                }
                else
                {
                    //For approvals, capture additional information about the task.
                    taskElement.submittedby = task.submittedby;
                    taskList = taskView.approvaltasks;
                }
                log.LogInformation($"Adding task {task.id} created by {task.submittedby} to TaskItemView for user {taskView.id}.");
                taskList.Add(taskElement);
            }
            else if(task.status.Equals("complete"))
            {
                //Loop through the pending tasks and remove the appropriate item.
                dynamic taskList = null;
                dynamic taskToRemove = null;

                 //Determine if this is a task submitted by the user, or an approval that needs to be removed and remove it from the right
                //area of the view.
                if (task.submittedby.Equals(taskView.id))
                {
                    taskList = taskView.mytasks;
                }
                else
                {
                    taskList = taskView.approvaltasks;
                }


                foreach(dynamic taskElement in taskList){
                    if (taskElement.id == task.id)
                    {
                        taskToRemove = taskElement;
                        break;
                    }
                }
                if (taskToRemove != null)
                {
                    log.LogInformation($"Removing task {task.id} created by {task.submittedby} from TaskItemView for user {taskView.id}.");
                    taskList.Remove(taskToRemove);
                }
            }
        }
    }
}
