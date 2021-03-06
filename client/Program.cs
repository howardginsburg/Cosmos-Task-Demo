﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.Tasks.Client
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Demo tasks = new Demo();
            await tasks.Run();
        }
    }

    class Demo
    {
        private string user = null;
        private RestHelper restHelper = new RestHelper();

        public async Task Run()
        {
            while (true)
            {
                Console.Clear();
                if (user == null)
                {
                    Console.WriteLine($"Task Demo");
                    Console.WriteLine($"-----------------------------------------------------------");
                }
                else
                {
                    Console.WriteLine($"Task Demo - Welcome {user}!");
                    Console.WriteLine($"-----------------------------------------------------------");
                   
                    int openTasks = 0;
                    int approvalTasks = 0;
                    dynamic view = await restHelper.GetTaskView(user);
                
                    if (view != null)
                    {
                        openTasks = view.mytasks.Count;
                        approvalTasks = view.approvaltasks.Count;
                    }
               
                    Console.WriteLine($"You have {openTasks} tasks opened.");
                    Console.WriteLine($"You have {approvalTasks} to approve.");
                    Console.WriteLine($"-----------------------------------------------------------");
                    Console.WriteLine($"[1]   Create new task");
                    if (openTasks > 0)
                    {
                        Console.WriteLine($"[2]   View my open tasks");
                    }
                    if (approvalTasks > 0)
                    {
                        Console.WriteLine($"[3]   Manage tasks I can approve");
                    }
                }

                Console.WriteLine($"[4]   Sample Data Generator");
                Console.WriteLine($"[5]   Logon/Logoff");
                Console.WriteLine($"[6]   Exit");

                int selection = -1;
                
                try
                {
                    selection = int.Parse(Console.ReadLine());                
                }
                catch (Exception ex)
                {

                }
               
                if (selection == 1)
                {
                    await HandleCreateNewTask();
                }
                else if (selection == 2)
                {
                    await HandleViewOpenTasks(true);
                }
                else if (selection == 3)
                {
                    await HandleViewOpenTasks(false);
                }
                else if (selection == 4)
                {
                    await HandleDataGenerator();
                }
                else if (selection == 5)
                {
                    HandleLogonoff();
                }
                else if (selection == 6)
                {
                    return;
                }
            }
        }

        private async Task HandleCreateNewTask()
        {
            Console.Clear();
            Console.WriteLine($"Task Demo - Create Task - {user}");
            Console.WriteLine($"-----------------------------------------------------------");
            Console.WriteLine($"What kind of task do you want to create?");
            Console.WriteLine($"[1]   Vacation");
            Console.WriteLine($"[2]   Invoice");
            
            int selection = int.Parse(Console.ReadLine());  

            //Build out the json payload.
            dynamic task = new ExpandoObject();
            task.status = "pending";
            task.submittedby = user;
            Console.WriteLine("What is the summary of the task?");
            task.summary = Console.ReadLine();
            Console.WriteLine("What is the detail text of the task?");
            task.detail = Console.ReadLine();
            if (selection == 1) //Vacation
            {
                task.type = "vacation";
                Console.WriteLine("What is the start date of the vacation (yyyy-mm-dd)?");
                task.start = Console.ReadLine();
                Console.WriteLine("What is the end date of the vacation (yyyy-mm-dd)?");
                task.end = Console.ReadLine();
            }
            else //Invoice
            {
                task.type = "invoice";
                Console.WriteLine("What is the id of the invoice?");
                task.invoiceid = Console.ReadLine();
                Console.WriteLine("What is the amount (just numbers) of the invoice?");
                task.amount = Console.ReadLine();
            }
            Console.WriteLine("What are the user names of the approvers (comma delimited)?");
            string approverList = Console.ReadLine();
            task.approvers = new List<ExpandoObject>();
            string[] approvers = approverList.Split(",");
            foreach(string approver in approvers)
            {
                dynamic taskApprover = new ExpandoObject();
                taskApprover.id = approver;
                //For simplicity of this demo, we're not going to do a lookup for a user name.
                taskApprover.name = $"{approver} full name";
                task.approvers.Add(taskApprover);
            }
            //Save the task.
            string id = await restHelper.SaveTask(task);
            Console.WriteLine($"New task ({id}) has been saved.  Press any key to continue...");
            Console.ReadKey(true);
    
        }

        private async Task HandleViewOpenTasks(bool myTasks)
        {
            //bool exit = false;
            while (true)
            {
                //Adjust the header based on if we're working on their tasks, or tasks they can approve.
                Console.Clear();
                if (myTasks)
                {
                    Console.WriteLine($"Task Demo - My Open Tasks - {user}");
                }
                else
                {
                    Console.WriteLine($"Task Demo - Approve Tasks - {user}");
                }
                Console.WriteLine($"-----------------------------------------------------------");
                dynamic view = await restHelper.GetTaskView(user);
                
                int count = 1;

                //Determine which set of tasks to loop through.
                dynamic tasks = null;
                if (myTasks)
                {
                    tasks = view.mytasks;
                }
                else
                {
                    tasks = view.approvaltasks;
                }
                foreach(dynamic taskElement in tasks){
                    //Build out the display of the task information.  Approval tasks have a submitted by field.
                    if (myTasks)
                    {
                        Console.WriteLine($"[{count}]   {taskElement.id}  {taskElement.type}  {taskElement.summary}");
                    }
                    else
                    {
                        Console.WriteLine($"[{count}]   {taskElement.id}  {taskElement.type}  {taskElement.submittedby}  {taskElement.summary}");
                    }
                    count++;
                }
                int back = -1;
                int approveAll = -1;
                
                if (!myTasks)
                {
                    approveAll = count++;
                    Console.WriteLine($"[{approveAll}]   Approve All");
                }
                back = count;
                Console.WriteLine($"[{back}]   Back");
                
                Console.WriteLine("What task would you like to view?");
                int selection = int.Parse(Console.ReadLine());  
                //If the selected 'Back', then return to the previous screen.
                if (selection == back)
                {
                   return;
                }
                else if (selection == approveAll) //All
                {
                    foreach(dynamic taskElement in tasks){
                        Console.WriteLine($"Marking task {taskElement.id} complete.");
                        dynamic task = await restHelper.GetTask(taskElement.id);
                        task.status = "complete";
                        await restHelper.SaveTask(task);
                    }
                    
                    Console.WriteLine($"Press any key to continue...");
                    Console.ReadKey();
                    return;
                }
                
                
                //Grab the task guid based on their choice (indexing starts at 0, so subtract 1), and open the task.
                await HandleViewTask(tasks[selection - 1].id);
            }
        }

        private async Task HandleViewTask(string guid)
        {
            Console.Clear();
            Console.WriteLine($"Task Demo - Task Detail - {user}");
            Console.WriteLine($"-----------------------------------------------------------");
            dynamic task = await restHelper.GetTask(guid);
            Console.WriteLine($"ID: {task.id}");
            Console.WriteLine($"Type: {task.type}");
            Console.WriteLine($"Summary: {task.summary}");
            Console.WriteLine($"Detail: {task.summary}");
            if (task.type == "vacation")
            {
                Console.WriteLine($"Start Date: {task.start}");
                Console.WriteLine($"End Date: {task.end}");
            }
            else
            {
                Console.WriteLine($"Invoice ID: {task.invoiceid}");
                Console.WriteLine($"Amount: ${task.amount}");
            }
            Console.WriteLine($"Approver(s):");
            {
                foreach(dynamic approver in task.approvers)
                {
                    Console.WriteLine($"  {approver.id}");
                }
            }
            Console.WriteLine();
            if (task.submittedby != user)
            {
                Console.WriteLine($"[1]   Mark Complete");
            }
            Console.WriteLine($"[2]   Back");
            

            int selection = int.Parse(Console.ReadLine());  
            if (selection == 1)
            {
                task.status = "complete";
                await restHelper.SaveTask(task);
                Console.WriteLine("This task has been marked complete!  Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        private void HandleLogonoff()
        {
            if (user == null)
            {
                Console.Clear();
                Console.WriteLine($"Task Demo");
                Console.WriteLine($"-----------------------------------------------------------");
                //If they haven't entered a user id that they want to simulate, prompt them.
                Console.WriteLine($"What is your user id?");
                user = Console.ReadLine();
            }
            else
            {
                user = null;
            }
        }

        private async Task HandleDataGenerator()
        {
            var random = new Random();
            string[] users = { "steve", "howard", "tim", "gina", "sarah", "kathy", "lori", "artie", "sam", "david", "brian", "mike" };
            string[] approvers = { "steve", "howard", "tim", "gina", "sarah", "kathy" };
            string[] taskType = {"vacation","invoice"};
            string[] status = {"pending","complete"};

            Console.Clear();
            Console.WriteLine($"Task Demo - Data Generator");
            Console.WriteLine($"-----------------------------------------------------------");
            //Get the number of tasks they want to create.
            Console.WriteLine($"How many tasks do you want to create?");
            int numberOfTasks = int.Parse(Console.ReadLine());

            DateTime currentDate = DateTime.UtcNow;
            for (int i = 0; i < numberOfTasks; i++)
            {
                dynamic task = new ExpandoObject();

                //Set the created date to sometime in the past year.
                DateTime createdDate = currentDate.AddDays(random.Next(-365,0));
                task.createddate = createdDate.ToString("o");

                task.type = taskType[random.Next(0, taskType.Length)];

                //We can create tasks in a 'complete' status for purposes of having the data
                //in the analytical store in synapse.
                task.status = status[random.Next(0, status.Length)];
                task.submittedby = users[random.Next(0, users.Length)];
                if (task.type == "vacation") //Vacation
                {
                    //Set the start date to sometime after the created date.
                    DateTime startDate = createdDate.AddDays(random.Next(5,180));
                    //Set the end date of the vacation request to within 10 days of the start date.
                    DateTime endDate = startDate.AddDays(random.Next(1,10));
                    task.start = startDate.ToString("yyyy-MM-dd");
                    task.end = endDate.ToString("yyyy-MM-dd");
                    task.summary = $"Vacation request from {task.start} to {task.end}";
                    
                }
                else //Invoice
                {
                    //Generate a random 15 character invoice id.
                    string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                    task.invoiceid = new string(Enumerable.Repeat(chars, 15).Select(s => s[random.Next(s.Length)]).ToArray());
                    //Generate a random amount for the invoice between $1 and $100000
                    task.amount = random.Next(1,100000);
                    task.summary = $"Invoice {task.invoiceid} for {task.amount}";
                }

                //Not worrying about task detail for the generator.
                task.detail = $"This was a generated request.";

                //Set the approvers to 1 or 2 people.
                task.approvers = new List<ExpandoObject>();
                //int numberOfApprovers = random.Next(1,2);
                //for (int j = 0; j < numberOfApprovers; j++)
                //{
                    //Get an approver, making sure we don't select the submitted by user.
                    string approver = approvers[random.Next(0, approvers.Length)];
                    while (approver == task.submittedby)
                    {
                        approver = approvers[random.Next(0, approvers.Length)];
                    }
                    
                    dynamic taskApprover = new ExpandoObject();
                    taskApprover.id = approver;
                    //For simplicity of this demo, we're not going to do a lookup for a user name.
                    taskApprover.name = $"{approver} full name";
                    task.approvers.Add(taskApprover);
                //}

                if (task.status == "complete")
                {
                    //Roll the createdDate forward by up to 60 days.
                    DateTime completedDate = createdDate.AddDays(random.Next(0,60));
                    //Add between 1 and 5 hours just in case the completed date is the same date as the created date.
                    //Otherwise reporting will make it look like it was approved at the same time it was created.
                    completedDate = completedDate.AddHours(random.Next(1,5));
                    task.completeddate = completedDate.ToString("o");
                }
                
                
                //Save the task.
                string id = await restHelper.SaveTask(task);
                Console.WriteLine($"Generated {i + 1} of {numberOfTasks} for user {task.submittedby} - {id}");
                
            }
            Console.WriteLine($"Data generator complete!  Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}
