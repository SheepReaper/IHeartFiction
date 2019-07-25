﻿/* Maintained By Johnathan P. Irvin
 *
 * ======== Disclaimer ========
 * This software may use 3rd party libraries, that have their own seperate licenses.
 * This software uses the GNU, General Public License (GPL v3) as defined at the link below.
 * https://www.gnu.org/licenses/gpl-3.0.en.html
 */

using MediatR;

namespace IHeartFiction.Domain.Events.Story
{
    public class ChapterTitleModified : INotification
    {
        public int StoryId { get; }
        public int ChapterId { get; }
        
        public string Title { get; }

        public ChapterTitleModified(int storyId, int chapterId, string title)
        {
            StoryId = storyId;
            ChapterId = chapterId;
            Title = title;
        }
    }
}
