using Common.Models;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.DataAccess
{
    public class PubSubTranscriptRepository
    {

        TopicName topicName;
        Topic topic;
        public PubSubTranscriptRepository(string projectId)
        {
            topicName = TopicName.FromProjectTopic(projectId, "transcriptions");
            if (topicName == null)
            {
                PublisherServiceApiClient publisher = PublisherServiceApiClient.Create();
                try
                {
                    topicName = new TopicName(projectId, "transcriptions");
                    topic = publisher.CreateTopic(topicName);
                }
                catch (Exception ex)
                {
                    //log
                    throw ex;
                }
            }
        }

        public async Task<string> PushMessage(Upload up)
        {

            PublisherClient publisher = await PublisherClient.CreateAsync(topicName);

            var pubsubMessage = new PubsubMessage
            {
                // The data is any arbitrary ByteString. Here, we're using text.
                Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(up)),
                // The attributes provide metadata in a string-to-string dictionary.
                Attributes =
                {
                    { "priority", "normal" }
                }
            };
            string message = await publisher.PublishAsync(pubsubMessage);
            return message;
        }

    }
}
