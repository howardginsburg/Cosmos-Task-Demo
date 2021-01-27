using System;
using System.Dynamic;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
//Cosmos V2 SDK.
using Microsoft.Azure.Documents;
//Cosmos V3 SDK.
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace Demo.TaskDemo
{
    /**
        TaskUpdateChangeFeed function that manages the task view for approvers.  As tasks change, the Cosmos changefeed will fire.
    */
    public class TaskUpdateChangeFeed
    {  
        
        private CosmosClient _cosmosClient;
        private Container _taskViewsContainer;
        public TaskUpdateChangeFeed(CosmosClient cosmosClient)
        {
            //Get the cosmos client object that our Startup.cs creates through dependency injection.
            _cosmosClient = cosmosClient;

            //Get the container we need to query.
            _taskViewsContainer = _cosmosClient.GetContainer("Tasks","TaskViews");
        }

        /**
            TaskUpdateChangeFeed function.

            This function is a Cosmos ChangeFeed trigger.  Cosmos bindings leverage the V2 SDK.  In our code we want to stick with
            the V3 SDK, so there is a blend here.
        */
        [FunctionName("TaskUpdateChangeFeed")]
        public async void Run([CosmosDBTrigger(
            databaseName: "Tasks",
            collectionName: "TaskItem",
            ConnectionStringSetting = "CosmosDBConnection",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)] IReadOnlyList<Document> input, 
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
                    
                    //Loop through all the approvers in the document.
                    foreach(dynamic approver in task.approvers)
                    {
                        //Get the TaskItemView for this approver.
                        dynamic taskView = await getTaskView(approver.id, log);

                        //Add or remove the approval from the users TaskItemView depending on the task status.
                        if (task.status.Equals("pending"))
                        {
                            log.LogInformation($"Adding new task {task.id} assignment for {approver.id}");
                            addApproval(taskView, task.id, task.submittedby);
                        }
                        else if (task.status.Equals("complete"))
                        {
                            log.LogInformation($"Removing task {task.id} assignment for {approver.id}");
                            removeApproval(taskView, task.id);
                        }

                        //Using the Cosmos V3 SDK, if they still have tasks to approve, go ahead and upsert the document.  Otherwise, delete it for good housekeeping.
                        if (taskView.tasks.Capacity > 0) 
                        {                    
                            var result = await _taskViewsContainer.UpsertItemAsync<Object>(item: taskView, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(taskView.id));
                            log.LogInformation($"Upserted TaskItemView for  {approver.id} with RU charge {result.RequestCharge}");
                        }
                        else
                        {
                            var result = await _taskViewsContainer.DeleteItemAsync<Object>(taskView.id, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(taskView.id));
                            log.LogInformation($"Deleted TaskItemView document for  {approver.id} as there are no remaining approvals with RU charge {result.RequestCharge}");
                        }
                        
                    }
                    
                }
            }

        }

        private async System.Threading.Tasks.Task<dynamic> getTaskView(dynamic id, ILogger log)
        {
            dynamic taskView = null;
            try{
                //Using the Comsos V3 SDK, read the TaskItemView as an Object type.  This allows us to not have to have a model class before turning it into a dynamic object.
                ItemResponse<Object> document = await _taskViewsContainer.ReadItemAsync<Object>(id: id, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(id));
                taskView = JsonConvert.DeserializeObject<ExpandoObject>(document.Resource.ToString(), new ExpandoObjectConverter());
            }
            catch (CosmosException ex)
            {
                //If we don't have a document in Cosmos for this user, a NotFound exception will be thrown and we can create an initial stub to use.
                if (ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                {
                    log.LogInformation($"User {id} does not have a TaskItemView.  A new document will be created.");
                    taskView = new ExpandoObject();
                    taskView.id = id;
                    taskView.tasks = new List<ExpandoObject>();
                }
                else{
                    throw;
                }

            }
            return taskView;
        }

        private void addApproval(dynamic taskView, dynamic taskid, dynamic submittedby)
        {
            //Create a new dynamic task for approval.
            dynamic task = new ExpandoObject();
            task.id = taskid;
            task.submittedby = submittedby;
            taskView.tasks.Add(task);
        }

        private void removeApproval(dynamic taskView, dynamic taskid)
        {
            //Loop through the pending tasks and remove the appropriate item.
            dynamic taskToRemove = null;
            foreach(dynamic task in taskView.tasks){
                if (task.id == taskid)
                {
                    taskToRemove = task;
                    break;
                }
            }
            if (taskToRemove != null)
            {
                taskView.tasks.Remove(taskToRemove);
            }
        }
    }
}
