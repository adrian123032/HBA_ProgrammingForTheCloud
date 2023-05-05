using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Microsoft.Extensions.Configuration;
using System.Threading;
using Common.Models;
using Newtonsoft.Json;
using CloudConvert.API;
using CloudConvert.API.Models.ExportOperations;
using CloudConvert.API.Models.ImportOperations;
using CloudConvert.API.Models.JobModels;
using CloudConvert.API.Models.TaskOperations;
using System.IO;
using Google.Cloud.Storage.V1;
using System.Net;

namespace SubscriberApp.Controllers
{
    public class SubscriberController : Controller
    {
        public async Task<IActionResult> Index([FromServices] IConfiguration config)
        {
            string projectId = config["projectid"].ToString();
            string subscriptionId = config["subscriptionId"].ToString();
            string apikey = config["apikey"].ToString();
            string bucket1 = config["bucket1"].ToString();
            bool acknowledge = false;

            SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
            SubscriberClient subscriber = await SubscriberClient.CreateAsync(subscriptionName);
            // SubscriberClient runs your message handle function on multiple
            // threads to maximize throughput.
            int messageCount = 0;

            List<string> messages = new List<string>();


            Task startTask = subscriber.StartAsync((PubsubMessage message, CancellationToken cancel) =>
            {
                string text = System.Text.Encoding.UTF8.GetString(message.Data.ToArray());
                messages.Add($"{message.MessageId}: {text}");

                Interlocked.Increment(ref messageCount);
                //if(acknowledge == true) { return SubscriberClient.Reply.Ack} else {return SubscriberClient.Reply.Nack}

                return Task.FromResult(acknowledge ? SubscriberClient.Reply.Ack : SubscriberClient.Reply.Nack);

                //acknowledgement implies that the message is going to be removed from the queue
                //no acknowledgement implies that the message is not going to be removed from the queue
            });
            // Run for 5 seconds.
            await Task.Delay(5000);
            await subscriber.StopAsync(CancellationToken.None);
            // Lets make sure that the start task finished successfully after the call to stop.
            await startTask;


            //evaluate the messages list

            foreach (var msg in messages.Distinct().ToList())
            {
                //send emails with the details
                var actualMessage = msg.Split(": ")[1];
                Upload myReadUpload = JsonConvert.DeserializeObject<Upload>(actualMessage);

                var CloudConvert = new CloudConvertAPI(apikey);


                var job = await CloudConvert.CreateJobAsync(new JobCreateRequest
                {
                    Tasks = new
                    {
                        import_it = new ImportUploadCreateRequest
                        {
                             
                        },
                        convert = new ConvertCreateRequest
                        {
                            Input = "import_it",
                            Input_Format = "mp4",
                            Output_Format = "flac"
                        },
                        export_it = new ExportUrlCreateRequest
                        {
                            Input = "convert"
                        }
                    }
                });

                var uploadTask = job.Data.Tasks.FirstOrDefault(t => t.Name == "import_it");
                var actualId = myReadUpload.BucketId.Split($"{bucket1}/")[1];
                var storage = StorageClient.Create();
                using (var Stream = new MemoryStream())
                {
                    storage.DownloadObject(bucket1, actualId, Stream);
                    await CloudConvert.UploadAsync(uploadTask.Result.Form.Url.ToString(), Stream, actualId, uploadTask.Result.Form.Parameters);
                }
                await CloudConvert.WaitJobAsync(job.Data.Id);

                var exportTask = job.Data.Tasks.FirstOrDefault(t => t.Name == "export_it");

                var fileExport = exportTask.Result.Files.FirstOrDefault();

                using (var client = new WebClient()) client.DownloadFile(fileExport.Url, fileExport.Filename);

            }
            return Content("Messages read and processed from queue: " + messageCount.ToString());
        }
    }
}
