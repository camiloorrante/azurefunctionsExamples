using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.IO;
using System;
using System.Text;

namespace Company.Function
{
    public static class GetRecipeAndUpload
    {
        [FunctionName("GetRecipeAndUpload")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            var outputs = new List<string>();
            
            //get recipe
            string recipe = await context.CallActivityAsync<string>("GetRecipeAndUpload_GetRecipe", null);
            //upload recipe to Azure blob container
            outputs.Add(await context.CallActivityAsync<string>("GetRecipeAndUpload_UploadBlob", recipe));

            
            return outputs;
        }


        [FunctionName("GetRecipeAndUpload_GetRecipe")]
        public static string GetRecipe([ActivityTrigger] string name, ILogger log)
        {
            var json = new WebClient().DownloadString("http://taco-randomizer.herokuapp.com/random/");
            return json.ToString();
        }

        [FunctionName("GetRecipeAndUpload_UploadBlob")]
        public static async void UploadBlob([ActivityTrigger] string recipe, ILogger log)
        {
            String connectionString = "DefaultEndpointsProtocol=https;AccountName=saksquare;AccountKey=e9VVpOtbnBzYY7DmsEvg1P5CyaP6OLN9Y/902NwbkQ5499cqc3rXVZ+ddQNzpYhw5oCzrUkUogk+ClhTAJwGdw==;EndpointSuffix=core.windows.net";
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

            //container name
            string containerName = "recipes";

            // Create the container and return a container client object
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            string fileName = "recipe" + Guid.NewGuid().ToString() + ".txt";
            // Get a reference to a blob
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            Console.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);

            using (MemoryStream ms = new MemoryStream())
            {
                var sw = new StreamWriter(ms);
                try
                {
                    sw.Write(recipe);
                    sw.Flush();//otherwise you are risking empty stream
                    ms.Seek(0, SeekOrigin.Begin);

                    // Test and work with the stream here. 
                    // If you need to start back at the beginning, be sure to Seek again.
                    await blobClient.UploadAsync(ms, true);
                }
                finally
                {
                    sw.Dispose();
                }

            }
        }
        [FunctionName("HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("GetRecipeAndUpload", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}