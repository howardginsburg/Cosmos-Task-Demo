using System.Dynamic;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
//Cosmos V2 SDK.
using Microsoft.Azure.Documents;
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
        private CosmosHelper _cosmosHelper;
        
        //CosmosNote - In order to easily get the CosmosHelper to read and update Tasks, we use dependency injection.  This is different from V2 where we can use
        //the Cosmos function binding to get the DocumentClient, and then build the CosmosHelper.
        public TaskUpdateChangeFeedFunction(CosmosHelper cosmosHelper)
        {
            //Get the cosmos client object that our Startup.cs creates through dependency injection.
            _cosmosHelper = cosmosHelper;
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
            CreateLeaseCollectionIfNotExists = true)] IReadOnlyList<Document> input, //CosmosNote - Document is a v2 sdk class definition.
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

                    //Get the TaskItemView for this submitter and update it accordingly.  If there is an etag discrepancy, we'll get a false back and need to try again.
                    bool taskOwnerViewResult = false;
                    while (taskOwnerViewResult == false)
                    {
                        dynamic taskOwnerView = await getTaskView(task.submittedby, log);
                        handleTaskApprovals(task, taskOwnerView, log);
                        taskOwnerViewResult = await saveTaskView(taskOwnerView, log);
                    }
                    
                    //Loop through all the approvers in the document and update them.  If there is an etag discrepancy, we'll get a false back and need to try again.
                    foreach(dynamic approver in task.approvers)
                    {
                        bool taskApproverResult = false;
                        while (taskApproverResult == false)
                        {
                            dynamic taskApproverView = await getTaskView(approver.id, log);
                            handleTaskApprovals(task, taskApproverView, log);
                            taskApproverResult = await saveTaskView(taskApproverView, log);
                        }
                    }
                }
            }

        }


        private async System.Threading.Tasks.Task<dynamic> getTaskView(dynamic id, ILogger log)
        {
            dynamic taskView = null;
            
            //Use the CosmosHelper to get the task view.
            dynamic document = await _cosmosHelper.ReadItemAsync("Tasks","TaskViews",id, log);
            if (document != null)
            {
                taskView = JsonConvert.DeserializeObject<ExpandoObject>(document.ToString(), new ExpandoObjectConverter());
            }
            else
            {
                log.LogInformation($"User {id} does not have a TaskItemView.  A new document will be created.");
                taskView = new ExpandoObject();
                taskView.id = id;
                taskView.mytasks = new List<ExpandoObject>();
                taskView.approvaltasks = new List<ExpandoObject>();
            }
            return taskView;
        }

        private async System.Threading.Tasks.Task<bool> saveTaskView(dynamic taskView, ILogger log)
        {
            //If they still have tasks to approve, go ahead and upsert the document.  Otherwise, delete it for good housekeeping.
            if ((taskView.mytasks.Count > 0) || (taskView.approvaltasks.Count > 0))
            {
                return await _cosmosHelper.UpsertItemAsync("Tasks","TaskViews",taskView,true,log);
            }
            else
            {
                return await _cosmosHelper.DeleteItemAsync("Tasks","TaskViews",taskView,true,log);
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