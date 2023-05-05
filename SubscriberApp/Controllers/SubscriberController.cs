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
using Google.Cloud.Speech.V1;
using Google.Cloud.Firestore;
using System.Text;
using Google.Cloud.Diagnostics.AspNetCore3;
using Microsoft.Extensions.Logging;

namespace SubscriberApp.Controllers
{
    public class SubscriberController : Controller
    {
        ILogger<SubscriberController> _logger;
        public SubscriberController(ILogger<SubscriberController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index([FromServices] IConfiguration config)
        {
            string projectId = config["projectid"].ToString();
            string subscriptionId = config["subscriptionId"].ToString();
            string apikey = config["apikey"].ToString();
            string bucket1 = config["bucket1"].ToString();
            string bucket2 = config["bucket2"].ToString();
            bool acknowledge = false;

            _logger.LogInformation($"Retrieved id's and keys from setting");

            SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
            SubscriberClient subscriber = await SubscriberClient.CreateAsync(subscriptionName);

            _logger.LogInformation($"Creating subscriberClient");
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
                var actualMessage = msg.Split(": ")[1];
                Upload myReadUpload = JsonConvert.DeserializeObject<Upload>(actualMessage);
                _logger.LogInformation($"Getting msg with BucketId: {myReadUpload.BucketId}");
                /*
                                var CloudConvert = new CloudConvertAPI(apikey);

                                var job = await CloudConvert.CreateJobAsync(new JobCreateRequest
                                {
                                    Tasks = new
                                    {
                                        import_it = new ImportUrlCreateRequest
                                        {
                                            Url = myReadUpload.BucketId
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

                                job = await CloudConvert.WaitJobAsync(job.Data.Id);
                                 _logger.LogInformation($"File sent to conversion API");

                                var exportTask = job.Data.Tasks.FirstOrDefault(t => t.Name == "export_it");

                                var fileExport = exportTask.Result.Files.FirstOrDefault();
                                _logger.LogInformation($"File {fileExport.FileName} retrieved back from API");

                                var webClient = new WebClient();
                                var fileStream = webClient.OpenRead(fileExport.Url);
                                var storage = StorageClient.Create();
                                storage.UploadObject(bucket2, fileExport.Filename, null, fileStream);
                                _logger.LogInformation($"File {fileExport.FileName} uploaded to {bucket2}");

                                */

                var speech = SpeechClient.Create();
                var configer = new RecognitionConfig
                {
                    Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
                    AudioChannelCount = 2,
                    LanguageCode = LanguageCodes.English.UnitedStates
                };
                var audio = RecognitionAudio.FromStorageUri($"gs://{bucket2}/f4027a29-8b81-44fc-9f0b-f15241b4969d.flac"); //{fileExport.Filename}");
                //_logger.LogInformation($"File {fileExport.FileName} retrieved from {bucket2} and given to Speech to Text API");

                var response = speech.Recognize(configer, audio);
                Upload up = new Upload();
                up = myReadUpload;
                var sb = new StringBuilder();
                int step = 1;
                string start = "00:00:00,000";
                foreach (var result in response.Results)
                {
                    foreach (var alternative in result.Alternatives)
                    {
                        
                        string end = result.ResultEndTime.ToString().Replace('.', ',');
                        end = end.Replace("{", "");
                        end = end.Replace("}", "");
                        end = end.Replace("\"", "");
                        _logger.LogInformation($"Transcript: {alternative} retrieved for next {end} and added to SRT Builder");
                        end = end.Replace("s", "");

                        string end1 = end.Split(",")[0];
                        string end2 = end.Split(",")[1];
                        // Add the first SRT entry
                        sb.AppendLine($"{step}");
                        step++;
                        sb.AppendLine($"{start} --> {start = new TimeSpan(0,0,0,int.Parse(end1),int.Parse(end2)).ToString(@"hh\:mm\:ss\,fff")}");
                        sb.AppendLine($"{alternative}");
                        sb.AppendLine();
                    }
                }

                up.Transcribed = true;
                FirestoreDb db =  FirestoreDb.Create(projectId);
                Query booksQuery = db.Collection("uploads").WhereEqualTo("BucketId", myReadUpload.BucketId);
                QuerySnapshot booksQuerySnapshot = await booksQuery.GetSnapshotAsync();

                DocumentSnapshot documentSnapshot = booksQuerySnapshot.Documents.FirstOrDefault();
                if (documentSnapshot.Exists == false) throw new Exception("Upload does not exist");
                else
                {
                    DocumentReference booksRef = db.Collection("uploads").Document(documentSnapshot.Id);
                    await booksRef.SetAsync(up);
                }


                /*Download Original Video
                var uploadTask = job.Data.Tasks.FirstOrDefault(t => t.Name == "import_it");
                var actualId = myReadUpload.BucketId.Split($"{bucket1}/")[1];
                var storage = StorageClient.Create();
                var stream = new MemoryStream();
          
                    storage.DownloadObject(bucket1, actualId, stream);

                using (var fileStream = new FileStream("your-file-name.mp4", FileMode.Create, FileAccess.Write))
                {
                    stream.WriteTo(fileStream);
                }*/


            }
            return Content("Messages read and processed from queue: " + messageCount.ToString());
        }
    }
}
