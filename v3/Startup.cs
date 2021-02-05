using System;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Demo.Task.V3.Startup))]

namespace Demo.Task.V3
{
    /**
        CosmosNote - Since Function bindings leverage the Cosmos V2 SDK, we take advantage of dependency injection to be able to get a handle to the
        V3 CosmosClient object.  Our V2 example does not use this at all.
    */
    public class Startup : FunctionsStartup
    {
        private static IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Register the CosmosClient as a Singleton

            builder.Services.AddSingleton((s) => {
                string connectionString = configuration["CosmosDBConnection"];
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentNullException("Please specify a valid CosmosDBConnection in the appSettings.json file or your Azure Functions Settings.");
                }

                CosmosClientBuilder configurationBuilder = new CosmosClientBuilder(connectionString);
                return configurationBuilder
                        .Build();
            });
        }
    }
}