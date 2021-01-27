Scenario Summary

This sample represents a task management system where a user can create tasks that are assigned to approvers. Approvers must be able to get a summary of the tasks that are assigned to them and then take action.

Requirements

1. The system must be able to support multiple types of tasks.  The data model for task types will be different and new task types must be able to be added with minimal effort.
2. Tasks can be assigned to 1..n approvers.
3. When a task is complete, it will be deleted from the system.
4. Approvers must be able to get a count of their open tasks.
5. Approvers must be able to get a summary of the tasks assigned to them.
6. All users must be able to get a list of tasks they created and are still open.
7. The system must be highly optimized to support 100's of tasks and approvals.

Architecture Overview

Prerequisites
1. Visual Studio Code or Visual Studio.
2. Configure to use Azure Functions.  Refer to https://docs.microsoft.com/en-us/azure/azure-functions/functions-develop-local.
3. Install and start the Azure Storage Emulator. Refer to https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator.
4. Postman or some other REST client.

Setup Steps
1. Create a Cosmos DB account.
2. Create a Database named Tasks with 400 request units.
3. Create a TaskItem collection with 'id' as the partition and database shared units.
4. Specify Time to Live as On(no default) on the TaskItem collection.
5. Create a TaskViews collection with 'id as the partition and database shared units.
6. To run locally, rename sample.settings.json to local.settings.json and add replace 'CosmosDBConnection' with you Cosmos account connection string.

Testing
1. 