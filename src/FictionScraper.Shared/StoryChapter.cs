using System;

namespace FictionScraper.Shared
{
    public class StoryChapter
    {
        public Guid ChapterGuid { get; } = Guid.NewGuid();
        public int ChapterSeq { get; set; }
        public Guid StoryGuid { get; set; } = Guid.Empty;
        public string HtmlContent { get; set; }
    }
}