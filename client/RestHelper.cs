using System;
using System.Dynamic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Demo.Tasks.Client
{
    class RestHelper
    {
        private readonly HttpClient client = new HttpClient();
        private readonly string GetTaskURI = null;
        private readonly string UpdateTaskURI = null;

        private readonly string GetTaskViewURI = null;
        public RestHelper()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json")
                .Build();
            
            GetTaskURI = configuration["GetTask"];
            GetTaskViewURI = configuration["GetTaskView"];
            UpdateTaskURI = configuration["UpdateTask"];
        }

        public async Task<dynamic> GetTask(string guid)
        {
            string uri = GetTaskURI.Replace("{id}",guid);
            string result = await client.GetStringAsync(uri);
            dynamic task = JsonConvert.DeserializeObject<ExpandoObject>(result, new ExpandoObjectConverter());
            
            return task;
        }

        public async Task<dynamic> GetTaskView(string user)
        {
            dynamic view = null;
            string uri = GetTaskViewURI.Replace("{id}",user);
            try
            {
                string result = await client.GetStringAsync(uri);
                view = JsonConvert.DeserializeObject<ExpandoObject>(result, new ExpandoObjectConverter());
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                if (ex.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw ex;
                }
            }
            
            //string formatted = JsonConvert.SerializeObject(task, Formatting.Indented);
            //Console.WriteLine(formatted);

            return view;
        } 

        public async Task<string> SaveTask(dynamic task)
        {
            string json = JsonConvert.SerializeObject(task, new ExpandoObjectConverter());
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage result = await client.PutAsync(UpdateTaskURI,content);
            string id = await result.Content.ReadAsStringAsync();
            return id;
        } 
    } 
}