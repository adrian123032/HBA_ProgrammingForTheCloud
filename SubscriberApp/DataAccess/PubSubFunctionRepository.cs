using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SubscriberApp.DataAccess
{

    public class PubSubFunctionRepository
    {

        TopicName topicName;
        Topic topic;
        public PubSubFunctionRepository(string projectId)
        {
            topicName = TopicName.FromProjectTopic(projectId, "ToSrtQueue");
            if (topicName == null)
            {
                PublisherServiceApiClient publisher = PublisherServiceApiClient.Create();
                try
                {
                    topicName = new TopicName(projectId, "ToSrtQueue");
                    topic = publisher.CreateTopic(topicName);
                }
                catch (Exception ex)
                {
                    //log
                    throw ex;
                }
            }
        }

        public async Task<string> PushId(string id)
        {

            PublisherClient publisher = await PublisherClient.CreateAsync(topicName);
            var pubsubMessage = new PubsubMessage
            {
            // The data is any arbitrary ByteString. Here, we're using text.
                Data = ByteString.CopyFromUtf8(id),
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
