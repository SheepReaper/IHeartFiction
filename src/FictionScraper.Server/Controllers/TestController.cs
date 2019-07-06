using System;
using System.Threading.Tasks;
using FictionScraper.Shared;
using Microsoft.AspNetCore.Mvc;

namespace FictionScraper.Server.Controllers
{
    [Route("[controller]")]
    public class TestController : Controller
    {

        [HttpGet("[action]")]
        public async Task<IActionResult> Exception()
        {
            await Task.CompletedTask;
            throw new StoryProviderException(RequestFailReason.UnexpectedResponseStatusCode);
            return BadRequest(new { ErrorMessage = "This is an error" });
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Headers()
        {
            await Task.CompletedTask;
            return Ok(this.Request.Headers["Accept"]);
        }
    }
}
