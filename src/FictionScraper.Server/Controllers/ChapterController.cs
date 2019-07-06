using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FictionScraper.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FictionScraper.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChapterController : ControllerBase
    {
        private readonly AppDbContext db;

        public ChapterController(AppDbContext context)
        {
            db = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<StoryChapter>>> GetAllChaptersAsync(CancellationToken ctx)
        {
            return Ok(await db.Chapters.ToListAsync(ctx));
        }

        [HttpGet("{guid}")]
        public async Task<ActionResult<StoryChapter>> GetChapterAsync(Guid guid, CancellationToken ctx = default)
        {
            var chapter = await db.Chapters.FindAsync(new object[] { guid }, ctx);

            return Ok(chapter);
        }
    }
}
