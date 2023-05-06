using CloudNative.CloudEvents;
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.PubSub.V1;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace PubSubFunction;

/// <summary>
/// A function that can be triggered in responses to changes in Google Cloud Storage.
/// The type argument (StorageObjectData in this case) determines how the event payload is deserialized.
/// The function must be deployed so that the trigger matches the expected payload type. (For example,
/// deploying a function expecting a StorageObject payload will not work for a trigger that provides
/// a FirestoreEvent.)
/// </summary>
    public class Function : ICloudEventFunction<MessagePublishedData>
    {
        private readonly ILogger<Function> _logger;

        public Function(ILogger<Function> logger) => _logger = logger; 

        public Task HandleAsync(CloudEvent cloudEvent, MessagePublishedData data, CancellationToken cancellationToken)
        {
            _logger.LogInformation("PubSub function has started executing");
            var FromMessage = data.Message?.TextData;
            _logger.LogInformation($"Data received is {FromMessage}");

            var name = string.IsNullOrEmpty(FromMessage) ? "world" : FromMessage;
            
            _logger.LogInformation($"Upload Id is {name}");

            FirestoreDb db = FirestoreDb.Create("hbaprogrammingforthecloud");
            DocumentReference docRef = db.Collection("uploads").Document(FromMessage);
            DocumentSnapshot docSnap = docRef.GetSnapshotAsync().Result;
            _logger.LogInformation($"Retrieving Upload Snapshot");

            string jsonString = JsonConvert.SerializeObject(docSnap.ToDictionary());
            _logger.LogInformation($"Serializing retrieved Upload");
            // Deserialize the JSON string to a dictionary
            Dictionary<string, object> docData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);

            // Access the values in the dictionary
            string transcription = docData["Transcription"].ToString();
            _logger.LogInformation($"Retrieved transcription: {transcription}");
            JsonDocument jsonDocument = JsonDocument.Parse(transcription);

            var sb = new StringBuilder();
            int step = 1;
            string start = "00:00:00,000";
            // Access the values in the JSON document
            JsonElement results = jsonDocument.RootElement.GetProperty("results");
            foreach (JsonElement result in results.EnumerateArray())
            {
                JsonElement alternatives = result.GetProperty("alternatives");
                foreach (JsonElement alternative in alternatives.EnumerateArray())
                {
                    _logger.LogInformation($"Building SRT string");
                    string transcript = alternative.GetProperty("transcript").GetString();
                    string resultEndTime = result.GetProperty("resultEndTime").GetString();
                        string end = resultEndTime.ToString().Replace('.', ',');
                        end = end.Replace("{", "");
                        end = end.Replace("}", "");
                        end = end.Replace("\"", "");
                        _logger.LogInformation($"Transcript: {transcript} retrieved for next {end} and added to SRT Builder");
                        end = end.Replace("s", "");

                        string end1 = end.Split(",")[0];
                        string end2 = end.Split(",")[1];
                        // Add the first SRT entry
                        sb.AppendLine($"{step}");
                        step++;
                        sb.AppendLine($"{start} --> {start = new TimeSpan(0,0,0,int.Parse(end1),int.Parse(end2)).ToString(@"hh\:mm\:ss\,fff")}");
                        sb.AppendLine($"{transcript}");
                        sb.AppendLine();

                }



            }
            Dictionary<string, object> update = new Dictionary<string, object>
            {
                { "Transcribed", true },
                { "Transcription", sb.ToString() }
            };
            var t = docRef.SetAsync(update, SetOptions.MergeAll);
            t.Wait();
            _logger.LogInformation($"Updated values to firestore");
            return Task.CompletedTask;
        }
    }

