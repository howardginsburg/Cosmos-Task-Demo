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
        CosmosNote - In order to use the ChangeFeed trigger, we must use V2 SDK. 
    */
    public class TaskUpdateChangeFeedFunction
    {

        
        private CosmosClient _cosmosClient;
        private Container _taskViewsContainer;
        //CosmosNote - In order to easily get the CosmosClient to read and update TaskViews, we use dependency injection.  This is different from V2 where we can use
        //the Cosmos function binding. 
        public TaskUpdateChangeFeedFunction(CosmosClient cosmosClient)
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

                    //Get the TaskItemView for this submitter.
                    dynamic taskOwnerView = await getTaskView(task.submittedby, log);
                    handleTaskApprovals(task, taskOwnerView, log);
                    saveTaskView(taskOwnerView, log);
                    
                    //Loop through all the approvers in the document.
                    foreach(dynamic approver in task.approvers)
                    {
                        //Get the TaskItemView for this approver.
                        dynamic taskApproverView = await getTaskView(approver.id, log);
                        handleTaskApprovals(task, taskApproverView, log);
                        saveTaskView(taskApproverView, log);
                    }
                    
                }
            }

        }


        private async System.Threading.Tasks.Task<dynamic> getTaskView(dynamic id, ILogger log)
        {
            dynamic taskView = null;
            try{
                //CosmosNote - Using the Cosmos V3 SDK, read the TaskItemView as an Object type.
                //This allows us to not have to have a model class before turning it into a dynamic object.
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
                    taskView.mytasks = new List<ExpandoObject>();
                    taskView.approvaltasks = new List<ExpandoObject>();
                }
                else{
                    throw;
                }

            }
            return taskView;
        }

        private async void saveTaskView(dynamic taskView, ILogger log)
        {
            //If they still have tasks to approve, go ahead and upsert the document.  Otherwise, delete it for good housekeeping.
            if ((taskView.mytasks.Count > 0) || (taskView.approvaltasks.Count > 0))
            {
                //CosmosNote - to upsert a document in V3, we need the object and the partition key.  V2 handles this differently requiring the URI for the document.                    
                var result = await _taskViewsContainer.UpsertItemAsync<Object>(item: taskView, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(taskView.id));
                log.LogInformation($"Upserted TaskItemView for  {taskView.id} with RU charge {result.RequestCharge}");
            }
            else
            {
                //CosmosNote - to delete the document in V3, we need the document id and partition key.  V2 handles this differently requiring the URI for the document and
                //partition key.
                var result = await _taskViewsContainer.DeleteItemAsync<Object>(taskView.id, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(taskView.id));
                log.LogInformation($"Deleted TaskItemView document for  {taskView.id} as there are no remaining approvals with RU charge {result.RequestCharge}");
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
