using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace FictionScraper.Shared
{
    [Serializable]
    public class StoryProviderException : Exception
    {
        public StoryProviderException()
        {
        }

        public StoryProviderException(string message) : base(message)
        {
        }

        public StoryProviderException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public StoryProviderException(RequestFailReason failReason) : base(SelectMessage(failReason))
        {
            FailureReasonCode = failReason;
        }

        public StoryProviderException(RequestFailReason failReason, StoryProvider storyProvider) : base(
            SelectMessage(failReason))
        {
            FailureReasonCode = failReason;
            StoryProvider = storyProvider;
        }

        public StoryProviderException(RequestFailReason failReason, StoryProvider storyProvider,
            Exception innerException) : base(SelectMessage(failReason), innerException)
        {
            FailureReasonCode = failReason;
            StoryProvider = storyProvider;
        }

        // Deserializer
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        protected StoryProviderException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            FailureReasonCode = (RequestFailReason) info.GetValue("FailureReasonCode", typeof(RequestFailReason));
            StoryProvider = (StoryProvider) info.GetValue("StoryProvider", typeof(StoryProvider));
        }

        public RequestFailReason FailureReasonCode { get; set; } = RequestFailReason.Unknown;
        public StoryProvider StoryProvider { get; set; } = new StoryProvider();

        // Serializer
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));

            info.AddValue("FailureReasonCode", FailureReasonCode);
            info.AddValue("StoryProvider", StoryProvider);

            base.GetObjectData(info, context);
        }

        public static string SelectMessage(RequestFailReason failReason)
        {
            switch (failReason)
            {
                case RequestFailReason.Timeout:
                    return "The remote server did not respond in a timely fashion, aborting story request.";
                case RequestFailReason.UnexpectedResponseStatusCode:
                    return
                        "The remote server hosting the story chapter you requested did not respond with an expected status code.";
                case RequestFailReason.UnexpectedResponseContent:
                    return
                        "Sorry, even though the remote server always responds with an OK status, the content received doesn't look like a story.";
                case RequestFailReason.Unknown:
                    return "A failure reason code was not provided, thus the reason is unknown";
                default:
                    return
                        $"The code provided: {failReason} does not have a standard response message defined"; // In case new reasons are added but not implemented
            }
        }
    }
}