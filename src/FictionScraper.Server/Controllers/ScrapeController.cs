using FictionScraper.Shared;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
//using Microsoft.OpenApi.Exceptions;
//using Swashbuckle.AspNetCore.Annotations;

namespace FictionScraper.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ScrapeController : ControllerBase
    {
        // GET: /story/fanfiction/{storyId}/{chapterNo}
        [HttpGet("fanfiction/{storyId}/{chapterNo}")]
        public async Task<IActionResult> GetStory(int storyId, int chapterNo = 1, CancellationToken ctx = default)
        {
            const string storyProviderName = "FanFiction.Net";
            const int fetchOperationTimeoutMilliSeconds = 4500;
            const string providerUrl = "https://www.fanfiction.net";

            var endpointFragment = $"/s/{storyId}/{chapterNo}";

            var storyProvider = new StoryProvider()
            {
                BaseUri = new Uri(providerUrl),
                UriFragment = new Uri(endpointFragment, UriKind.Relative),
                FetchRequestTimeoutMilliSeconds = fetchOperationTimeoutMilliSeconds,
                Name = storyProviderName
            };

            try
            {
                var htmlDoc = await storyProvider.LoadFromWebAsync(ctx);

                if (storyProvider.LastStatusCodeReceived != HttpStatusCode.OK)
                    throw new StoryProviderException(RequestFailReason.UnexpectedResponseStatusCode, storyProvider);

                var nodes = htmlDoc.DocumentNode.SelectNodes("//div").Where(n => n.HasClass("panel_normal"));

                if (nodes.Any())
                    throw new StoryProviderException(RequestFailReason.UnexpectedResponseContent, storyProvider);

                var chapterData = new StoryChapter
                {
                    ChapterSeq = chapterNo,
                    HtmlContent = htmlDoc.GetElementbyId("storytextp").OuterHtml
                };

                return Request.Headers["Accept"].FirstOrDefault()?.Split(',').FirstOrDefault() == "text/html"
                    ? Ok(chapterData.HtmlContent)
                    : Ok(chapterData);
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case OperationCanceledException _:
                    case StoryProviderException _:
                        return BadRequest(ex);
                    default:
                        throw;
                }
            }
        }

        //[ProducesResponseType(typeof(List<IndexResult>), 200)]
        //[ProducesResponseType(typeof(OperationCanceledException), 400)]
        //[ProducesResponseType(typeof(StoryProviderException), 400)]
        // GET: /story/fanfiction/search/
        [HttpGet("fanfiction/search/")]
        //[SwaggerResponse(200, "", typeof(IEnumerable<IndexResult>))]
        //[SwaggerResponse(400, "", typeof(ErrorResponse))]
        //[SwaggerResponse(400, "", typeof(WeatherForecast))]
        public async Task<IActionResult> FindStories([FromQuery] string keywords, [FromQuery] int page = 1, CancellationToken ctx = default)
        {
            const string storyProviderName = "FanFiction.Net";
            const int fetchOperationTimeoutMilliSeconds = 4500;
            const string providerUrl = "https://www.fanfiction.net";

            var endpointFragment = $"/search/?ready=1&keywords={keywords}&type=story&ppage={page}";

            var storyProvider = new StoryProvider
            {
                BaseUri = new Uri(providerUrl),
                UriFragment = new Uri(endpointFragment, UriKind.Relative),
                FetchRequestTimeoutMilliSeconds = fetchOperationTimeoutMilliSeconds,
                Name = storyProviderName
            };

            try
            {
                var htmlDoc = await storyProvider.LoadFromWebAsync(ctx);

                if (storyProvider.LastStatusCodeReceived != HttpStatusCode.OK)
                    throw new StoryProviderException(RequestFailReason.UnexpectedResponseStatusCode, storyProvider);

                var nodes = htmlDoc.DocumentNode.SelectNodes("//div").Where(n => n.HasClass("z-list"));

                var stories = nodes.Select((node, index) => new IndexResult { Content = node.InnerHtml, Id = index });

                return stories.Any()
                    ? Ok(stories)
                    : throw new StoryProviderException(RequestFailReason.UnexpectedResponseContent, storyProvider);
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case OperationCanceledException _:
                    case StoryProviderException _:
                        return BadRequest(ex);
                    default:
                        throw;
                }
            }
        }
    }

    public class IndexResult
    {
        public int Id { get; set; }
        public string Content { get; set; }
    }
}