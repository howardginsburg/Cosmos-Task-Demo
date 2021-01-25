using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System.Security;

namespace Demo.Task
{
    public class TaskUpdateChangeFeed
    {  
        private CosmosClient _cosmosClient;
        private Container _taskViewsContainer;
        public TaskUpdateChangeFeed(CosmosClient cosmosClient)
        {
            _cosmosClient = cosmosClient;
            _taskViewsContainer = _cosmosClient.GetContainer("Tasks","TaskViews");
        }

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
            				
            if (input != null && input.Count > 0)
            {
                log.LogInformation("Documents modified " + input.Count);

                foreach (Document document in input)
                {
                    dynamic task = JsonConvert.DeserializeObject<JObject>(document.ToString());
                    //string payload = Convert.ToString(task);
                    //log.LogInformation(document.ToString());
                    string taskid = task.id;
                    string status = task.status;
                    if (status.Equals("pending"))
                    {
                        dynamic approvers = task.approvers;
                        foreach(dynamic approver in approvers)
                        {
                            string userid = approver.id;
                            string username = approver.name;
                            log.LogInformation($"Adding new task {taskid} assignment for {userid}");
                            
                            
                            dynamic userView = null;
                            try{
                                dynamic userDoc = await _taskViewsContainer.ReadItemAsync<Object>(id: userid, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(userid));
                                // docUri = UriFactory.CreateDocumentUri("Tasks", "TaskViews", userid);
                                //PartitionKey partitionKey = new PartitionKey(userid) ;
                                //RequestOptions requestOptions = new RequestOptions() { PartitionKey = partitionKey };
                                //var userDoc = await client.ReadDocumentAsync(docUri, requestOptions);
                                userView = JsonConvert.DeserializeObject<JObject>(userDoc.ToString());
                            }
                            catch (CosmosException ex)
                            {
                                if (ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                                {
                                    log.LogInformation($"Creating new task view document for {userid}");
                                    userView = new JObject();
                                    userView.id = userid;
                                }
                                else{
                                    throw;
                                }

                            }
                           
                            await _taskViewsContainer.UpsertItemAsync(item: userView, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(userid));
                            
                        }
                    }
                    
                }
            }
        }
    }
}
