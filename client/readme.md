# Overview
To simplify trying out the Functions project, you can run this sample console app which provides the following functionality:
1. A data generator to load the system.  Note, if you use this, it is advised that you have deployed your Cosmos database with Autoscale.
2. The ability to simimulate logging in as different users to view/approve tasks.
3. A wizard that will allow you to create a task.

### Usage

1. Open the DemoClient.csproj in VS Code of Visual Studio.
2. Rename sample.settings.json as local.settings.json.
3. Edit local.settings.json and replace with the url of your functions.  By default, it contains the urls of the functions running locally.
4. Run the app and navigate through your choice of behavior.

### Data Generator
1. The data generator is designed to simulate a large of amount of tasks being generated and completed.  This is helpful when using the analytical store for Cosmos with Synapse.