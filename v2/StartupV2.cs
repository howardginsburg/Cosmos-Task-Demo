using System;
using System.Data.Common;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Demo.Task.V2.StartupV2))]

namespace Demo.Task.V2
{
    /**
        There is no way to get a DocumentClient using the Function bindings.  So,
        Dependency injection startup class that our Functions will take advantage of to get a Document Client object.
    */
    public class StartupV2 : FunctionsStartup
    {
        private static IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Register the CosmosClient as a Singleton
            string connectionString = configuration["CosmosDBConnection"];
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("Please specify a valid CosmosDBConnection in the appSettings.json file or your Azure Functions Settings.");
            }

            // Use this generic builder to parse the connection string and get the values we need for the constructor of DocumentClient.
            DbConnectionStringBuilder connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            object key = null;
            object uri = null;
            connectionBuilder.TryGetValue("AccountKey", out key);
            connectionBuilder.TryGetValue("AccountEndpoint", out uri);

            string accountKey = key.ToString();
            Uri serviceEndpoint = new Uri(uri.ToString());

            IDocumentClient client = new DocumentClient(serviceEndpoint,accountKey);

            //Add the client so we can get it injected into the function constructor.
            builder.Services.AddSingleton<IDocumentClient>(client);
        }
    }
}