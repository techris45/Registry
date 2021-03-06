﻿using System;
using Microsoft.EntityFrameworkCore;
using Registry.Web.Data.Models;

namespace Registry.Web.Data
{
    public class RegistryContext: DbContext
    {
        public RegistryContext(DbContextOptions<RegistryContext> options)
            : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Dataset>()
                .HasIndex(ds => ds.Slug);

            modelBuilder.Entity<FileChunk>()
                .HasOne(chunk => chunk.Session)
                .WithMany(session => session.Chunks)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DownloadPackage>()
                .Property(e => e.Paths)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries));

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
#endif
        }

        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Dataset> Datasets { get; set; }

        public DbSet<Batch> Batches { get; set; }
        public DbSet<Entry> Entries { get; set; }

        public DbSet<UploadSession> UploadSessions { get; set; }
        public DbSet<FileChunk> FileChunks { get; set; }

        public DbSet<DownloadPackage> DownloadPackages { get; set; }

    }
}
