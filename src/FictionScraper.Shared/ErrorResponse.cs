using System;
using System.Net;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace FictionScraper.Shared
{
    [DataContract]
    public class ErrorResponse
    {
        private readonly bool ExposeDebugObject = true;

        public ErrorResponse() : this(HttpStatusCode.BadRequest, "", null)
        {
        }

        public ErrorResponse(Exception ex)
        {
            StatusCode = HttpStatusCode.OK;
            Message = ex.Message;
            //Exception = ex;
        }

        public ErrorResponse(Exception ex, StoryProvider storyProvider) : this(ex)
        {
        }

        public ErrorResponse(OperationCanceledException ex, StoryProvider storyProvider) : this(
            new StoryProviderException(RequestFailReason.Timeout, storyProvider, ex))
        {
        }

        public ErrorResponse(OperationCanceledException ex) : this(ex, new StoryProvider())
        {
        }

        public ErrorResponse(HttpStatusCode statusCode, string message, Exception ex)
        {
            StatusCode = statusCode;
            Message = message;
            //Exception = ex;
        }

        [IgnoreDataMember] public DebugInfo Debug { get; set; }

        [DataMember(Name = "debug", EmitDefaultValue = false)]
        public DebugInfo SerializedDebugInfo => ExposeDebugObject ? Debug : null;

        [DataMember] public HttpStatusCode StatusCode { get; set; }

        [DataMember] public string Reason => ReasonPhrases.GetReasonPhrase((int) StatusCode);

        [DataMember] public string Message { get; set; }
    }
}