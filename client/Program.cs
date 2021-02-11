using System;
using System.Collections.Generic;
using System.Dynamic;
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

                Console.WriteLine($"[4]   Logon/Logoff");
                Console.WriteLine($"[5]   Exit");

                ConsoleKeyInfo result = Console.ReadKey(true);

                
               
                if (result.KeyChar == '1')
                {
                    await HandleCreateNewTask();
                }
                else if (result.KeyChar == '2')
                {
                    await HandleViewOpenTasks(true);
                }
                else if (result.KeyChar == '3')
                {
                    await HandleViewOpenTasks(false);
                }
                else if (result.KeyChar == '4')
                {
                    HandleLogonoff();
                }
                else if (result.KeyChar == '5')
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
            ConsoleKeyInfo taskType = Console.ReadKey(true);

            //Build out the json payload.
            dynamic task = new ExpandoObject();
            task.status = "pending";
            task.submittedby = user;
            Console.WriteLine("What is the summary of the task?");
            task.summary = Console.ReadLine();
            Console.WriteLine("What is the detail text of the task?");
            task.detail = Console.ReadLine();
            if (taskType.KeyChar == '1') //Vacation
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
                taskApprover.name = $"{approver} (name value)";
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
                Console.WriteLine($"[B]   Back");
                Console.WriteLine("What task would you like to view?");
                ConsoleKeyInfo key = Console.ReadKey(true);
                //If the selected 'Back', then return to the previous screen.
                if (key.KeyChar.ToString().ToUpper() == "B")
                {
                   return;
                }
                int choice = int.Parse(key.KeyChar.ToString());
                
                //Grab the task guid based on their choice (indexing starts at 0, so subtract 1), and open the task.
                await HandleViewTask(tasks[choice - 1].id);
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
                Console.WriteLine($"[A]   Mark Complete");
            }
            Console.WriteLine($"[B]   Back");
            

            ConsoleKeyInfo result = Console.ReadKey(true);
            if (result.KeyChar.ToString().ToUpper() == "A")
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
    }
}
