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
using SubscriberApp.DataAccess;
using Common.DataAccess;

namespace SubscriberApp.Controllers
{
    public class SubscriberController : Controller
    {
        ILogger<SubscriberController> _logger;
        PubSubTranscriptRepository _psRepository;
        PubSubFunctionRepository _psfRepository;
        public SubscriberController(ILogger<SubscriberController> logger, PubSubFunctionRepository psfRepository, PubSubTranscriptRepository psRepository)
        {
            _logger = logger;
            _psfRepository = psfRepository;
            _psRepository = psRepository;
        }

        public async Task<IActionResult> Index([FromServices] IConfiguration config)
        {
            string projectId = config["projectid"].ToString();
            string subscriptionId = config["subscriptionId"].ToString();
            string apikey = config["apikey"].ToString();
            string bucket1 = config["bucket1"].ToString();
            string bucket2 = config["bucket2"].ToString();
            bool acknowledge = true;

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
            await Task.Delay(10000);
            await subscriber.StopAsync(CancellationToken.None);
            // Lets make sure that the start task finished successfully after the call to stop.
            await startTask;


            //evaluate the messages list

            foreach (var msg in messages.Distinct().ToList())
            {
                var actualMessage = msg.Split(": ")[1];
                Upload myReadUpload = JsonConvert.DeserializeObject<Upload>(actualMessage);
                _logger.LogInformation($"Getting msg with BucketId: {myReadUpload.BucketId}");
                
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
                _logger.LogInformation($"File {fileExport.Filename} retrieved back from API");

                var webClient = new WebClient();
                var fileStream = webClient.OpenRead(fileExport.Url);
                var storage = StorageClient.Create();
                storage.UploadObject(bucket2, fileExport.Filename, null, fileStream);
                _logger.LogInformation($"File {fileExport.Filename} uploaded to {bucket2}");

                                

                var speech = SpeechClient.Create();
                var configer = new RecognitionConfig
                {
                    Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
                    AudioChannelCount = 2,
                    LanguageCode = LanguageCodes.English.UnitedStates
                };
                var audio = RecognitionAudio.FromStorageUri($"gs://{bucket2}/{fileExport.Filename}");
                //_logger.LogInformation($"File {fileExport.FileName} retrieved from {bucket2} and given to Speech to Text API");

                var response = speech.Recognize(configer, audio);
                Upload up = new Upload();
                up = myReadUpload;
                var sb = new StringBuilder();
                int step = 1;
                string start = "00:00:00,000";
                foreach (var result in response.Results)
                {
                    up.Transcription = response.ToString();
                    foreach (var alternative in result.Alternatives)
                    {                       
                    }
                }
                up.Queued = true;
                FirestoreDb db =  FirestoreDb.Create(projectId);
                Query uploadsQuery = db.Collection("uploads").WhereEqualTo("BucketId", myReadUpload.BucketId);
                QuerySnapshot uploadsQuerySnapshot = await uploadsQuery.GetSnapshotAsync();

                DocumentSnapshot documentSnapshot = uploadsQuerySnapshot.Documents.FirstOrDefault();
                if (documentSnapshot.Exists == false) throw new Exception("Upload does not exist");
                else
                {
                    DocumentReference uploadsRef = db.Collection("uploads").Document(documentSnapshot.Id);
                    await uploadsRef.SetAsync(up);
                    await _psfRepository.PushId(documentSnapshot.Id);
                }

                string bucketName = config["bucket2"].ToString();
                storage.DeleteObject(bucketName, fileExport.Filename);


                }
            return Content("Messages read and processed from queue: " + messageCount.ToString());
        }
    }
}
