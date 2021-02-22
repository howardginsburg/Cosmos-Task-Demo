using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Client;
using System.Dynamic;
using Newtonsoft.Json.Converters;

namespace Demo.TaskDemo
{
    
    /**
        BaseFunction function.
        
    */
    public class CosmosHelper
    {
        //Reference to the DocumentClient.                   
        private DocumentClient _cosmosClient = null;

        //CosmosNote - The v2 example passes in the DocumentClient which is obtained using function bindings.
        //The v3 constructor takes in the connection string and then instantiates the CosmosClient.  
        public CosmosHelper(DocumentClient client)
        {
            _cosmosClient = client;
        }


        public async Task<dynamic> ReadItemAsync(string database, string collection, string id, ILogger log)
        {
            //CosmosNote - we must retieve the document through code.  This is fairly simple, but not as elegant as the function binding in V2.
            //Use dynamic so we don't need to have a model object.
            dynamic item = null;
            try{
                //CosmosNote - Using the Comsos V2 SDK, read the TaskItemView as a Document type.  
                //This allows us to not have to have a model class before turning it into a dynamic object.
                RequestOptions options = new RequestOptions();
                options.PartitionKey = new PartitionKey(id);

                Uri uri = UriFactory.CreateDocumentUri(database,collection,id);
                Document document = await _cosmosClient.ReadDocumentAsync(uri, options);
                item = JsonConvert.DeserializeObject<Object>(document.ToString());
            }
            catch (DocumentClientException ex)
            {
                //If we don't have a document in Cosmos for this user, a NotFound exception will be thrown and we can create an initial stub to use.
                if (!ex.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                {
                    throw;
                }

            }
            return item;

        }

        public async Task<bool> UpsertItemAsync(string database, string collection, dynamic item, bool checkETag, ILogger log)
        {
            bool result = false;
            try
            {
                
                RequestOptions options = new RequestOptions();
                options.PartitionKey = new PartitionKey(item.id);

                //If this is an existing document, it will have an etag.  Lets make sure we don't overwrite the view if
                //it was updated by a parallel function running.                
                if (((IDictionary<String, object>)item).ContainsKey("_etag") && checkETag)
                {
                    AccessCondition accessCondition = new AccessCondition();
                    accessCondition.Condition = item._etag;
                    accessCondition.Type = AccessConditionType.IfMatch;
                    options.AccessCondition = accessCondition;
                }

                Uri uri = UriFactory.CreateDocumentCollectionUri("Tasks","TaskViews");
                var response = await _cosmosClient.UpsertDocumentAsync(uri, item,options);
                log.LogInformation($"Upserted {item.id} from {collection} with RU charge {response.RequestCharge}");
                result = true;
            }
            catch (DocumentClientException ex)
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
                RequestOptions options = new RequestOptions();
                options.PartitionKey = new PartitionKey(item.id);

            //If this is an existing document, it will have an etag.  Lets make sure we don't overwrite the view if
            //it was updated by a parallel function running.                
            if (((IDictionary<String, object>)item).ContainsKey("_etag"))
            {
                AccessCondition accessCondition = new AccessCondition();
                accessCondition.Condition = item._etag;
                accessCondition.Type = AccessConditionType.IfMatch;
                options.AccessCondition = accessCondition;
            }
                Uri uri = UriFactory.CreateDocumentUri(database,collection,item.id);
                var response = await _cosmosClient.DeleteDocumentAsync(uri,options);
                log.LogInformation($"Deleted {item.id} from {collection} with RU charge {response.RequestCharge}");
                result = true;
            }
            catch (DocumentClientException ex)
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
