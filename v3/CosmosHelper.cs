using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Demo.TaskDemo
{
    
    /**
        BaseFunction function.
        
    */
    public class CosmosHelper
    {
        //Reference to the CosmosClient.                   
        private CosmosClient _cosmosClient = null;


        //CosmosNote - the v3 constructor takes in the connection string and then instantiates the CosmosClient.  The v2 example passes in the DocumentClient
        //which is obtained using function bindings.
        public CosmosHelper(string ConnectionString)
        {
            _cosmosClient = new CosmosClient(ConnectionString);
        }

        /**
        * Read a single document from the collection using a point read.
        */
        public async Task<dynamic> ReadItemAsync(string database, string collection, string id, ILogger log)
        {
            
            dynamic item = null;
            try
            {
                Container container = _cosmosClient.GetContainer(database, collection);
                //Query for the document an Object so we don't need to have a model class defined.
                ItemResponse<Object> response = await container.ReadItemAsync<Object>(id: id, partitionKey: new PartitionKey(id));
                log.LogInformation($"Retrieved {id} from {collection} with RU charge {response.RequestCharge}");

                //Convert the document to json, which is located in the Resource parameter and needs to be a string
                item = JsonConvert.DeserializeObject<Object>(response.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                //Handle document not found.  We will return a null.
                if (!ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                {
                    throw ex;
                }
            }
            return item;

        }

        public async Task<bool> UpsertItemAsync(string database, string collection, dynamic item, bool checkETag, ILogger log)
        {
            bool result = false;
            try
            {
                
                ItemRequestOptions options = null;

                //If this is an existing document, it will have an etag.  Lets make sure we don't overwrite the view if
                //it was updated by a parallel function running.                
                if (((IDictionary<String, object>)item).ContainsKey("_etag") && checkETag)
                {
                    options = new ItemRequestOptions { IfMatchEtag = item._etag };
                }

                Container container = _cosmosClient.GetContainer(database, collection);
                ItemResponse<Object> response = await container.UpsertItemAsync<Object>(item: item, partitionKey: new PartitionKey(item.id), requestOptions: options);
                log.LogInformation($"Upserted {item.id} from {collection} with RU charge {response.RequestCharge}");
                result = true;
            }
            catch (CosmosException ex)
            {
                //If the document etag did not match, retrieve again and try again.
                if (!ex.StatusCode.Equals(System.Net.HttpStatusCode.PreconditionFailed))
                {
                    throw ex;
                }

            }

            return result;
        }

        public async Task<bool> DeleteItemAsync(string database, string collection, dynamic item, bool checkETag, ILogger log)
        {
            bool result = false;
            try
            {
                ItemRequestOptions options = null;

                //If this is an existing document, it will have an etag.  Lets make sure we don't overwrite the view if
                //it was updated by a parallel function running.                
                if (((IDictionary<String, object>)item).ContainsKey("_etag") && checkETag)
                {
                    options = new ItemRequestOptions { IfMatchEtag = item._etag };
                }
                Container container = _cosmosClient.GetContainer(database, collection);
                ItemResponse<Object> response = await container.DeleteItemAsync<Object>(item.id, partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(item.id),requestOptions: options);
                log.LogInformation($"Deleted {item.id} from {collection} with RU charge {response.RequestCharge}");
                result = true;
            }
            catch (CosmosException ex)
            {
                //If the document etag did not match, return false so the caller knows the delete did not happen.
                if (ex.StatusCode.Equals(System.Net.HttpStatusCode.PreconditionFailed))
                {
                    result = false;
                }
                //If the document was not found, that's okay.
                else if (ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                {
                    result = true;
                }
                else
                {
                    throw ex;
                }
            }
            return result;
        }
    }
}
