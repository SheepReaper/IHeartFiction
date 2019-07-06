using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FictionScraper.Shared;
using Microsoft.EntityFrameworkCore;

namespace FictionScraper.Server
{
    public class SeedData
    {
        public static void SeedDatabase(AppDbContext context)
        {
            var chapters = new StoryChapter[]
            {
                new StoryChapter()
                {
                    ChapterSeq = 1,
                    HtmlContent = "<h1>BOO!</h1>",
                    StoryGuid = Guid.NewGuid()
                }
            };

            context.Chapters.AddRange(chapters);
            context.SaveChanges();
        }
    }
}
