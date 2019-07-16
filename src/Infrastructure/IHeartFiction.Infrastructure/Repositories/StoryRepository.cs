﻿/* Maintained By Johnathan P. Irvin
 *
 * ======== Disclaimer ========
 * This software may use 3rd party libraries, that have their own seperate licenses.
 * This software uses the GNU, General Public License (GPL v3) as defined at the link below.
 * https://www.gnu.org/licenses/gpl-3.0.en.html
 */

using IHeartFiction.Domain.AggregateModels.StoryAggregate;
using IHeartFiction.Domain.SeedWork;
using System;

namespace IHeartFiction.Infrastructure.Repositories
{
    public class StoryRepository : IStoryRepository
    {
        private FictionContext _context;

        public IUnitOfWork UnitOfWork => _context;
    }
}