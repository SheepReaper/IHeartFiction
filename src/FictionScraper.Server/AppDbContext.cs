using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FictionScraper.Shared;
using Microsoft.EntityFrameworkCore;

namespace FictionScraper.Server
{
    public class AppDbContext : DbContext
    {
        public DbSet<StoryChapter> Chapters { get; set; }
        public AppDbContext(DbContextOptions options) : base(options)
        {
            
        }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    if (optionsBuilder.IsConfigured) return;

        //    //optionsBuilder.
        //}
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StoryChapter>().HasKey(c => c.ChapterGuid);
        }
    }
}
