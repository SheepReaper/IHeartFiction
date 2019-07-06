namespace FictionScraper.Shared
{
    public enum RequestFailReason
    {
        Timeout,
        UnexpectedResponseStatusCode,
        UnexpectedResponseContent,
        Unknown
    }
}