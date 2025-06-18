using BookKeepingWeb.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookKeepingWeb.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<Tag> Tags { get; set; } //this line might be required, ask romeo
        public DbSet<UploadContent> UploadContents { get; set; } // This line is required
        public DbSet<UserProfile> UserProfiles { get; set; } // Add this line
        public DbSet<Comment> Comments { get; set; } // Add this line

        public DbSet<CommentReport> CommentReports { get; set; }

        public DbSet<TakedownRequestModel> takedownRequests { get; set; } //this line is required thank romeo for this

        public DbSet<ForumThread> ForumThreads { get; set; }
        public DbSet<ForumComment> ForumComments { get; set; }

        public DbSet<UploadContentRelation> UploadContentRelation { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UploadContentRelation>()
                .HasKey(uc => new { uc.UploadContentId, uc.RelatedContentId });

            modelBuilder.Entity<UploadContentRelation>()
                .HasOne(uc => uc.UploadContent)
                .WithMany(u => u.RelatedContents)
                .HasForeignKey(uc => uc.UploadContentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UploadContentRelation>()
                .HasOne(uc => uc.RelatedContent)
                .WithMany()
                .HasForeignKey(uc => uc.RelatedContentId)
                .OnDelete(DeleteBehavior.Restrict);

            //  One UserProfile -> Many Comments
            modelBuilder.Entity<ForumComment>()
                .HasOne(fc => fc.CreatedByUser)
                .WithMany()
                .HasForeignKey(fc => fc.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            //  One ForumThread -> Many Comments
            modelBuilder.Entity<ForumComment>()
                .HasOne(fc => fc.Thread)
                .WithMany(ft => ft.Comments)
                .HasForeignKey(fc => fc.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            // One ForumComment -> Many Reports (New)
            modelBuilder.Entity<CommentReport>()
                .HasOne(cr => cr.ForumComment)
                .WithMany()
                .HasForeignKey(cr => cr.ForumCommentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ForumThread>()
                .HasOne(ft => ft.CreatedByUser)
                .WithMany()
                .HasForeignKey(ft => ft.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);


            // Relationship: One Comment -> Many Reports
            modelBuilder.Entity<CommentReport>()
                .HasOne(cr => cr.Comment)
                .WithMany()
                .HasForeignKey(cr => cr.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: One UserProfile -> Many CommentReports (Users reporting comments)
            modelBuilder.Entity<CommentReport>()
                .HasOne(cr => cr.ReportedByUser)
                .WithMany()
                .HasForeignKey(cr => cr.ReportedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Define the one-to-many relationship
            modelBuilder.Entity<UploadContent>()
                .HasOne(uc => uc.UserProfile)
                .WithMany(up => up.UploadContents)
                .HasForeignKey(uc => uc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: One UploadContent -> Many Comments
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.UploadContent)
                .WithMany(uc => uc.Comments)
                .HasForeignKey(c => c.UploadContentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: One UserProfile -> Many Comments
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Define the many-to-many relationship between UploadContent and Tag
            modelBuilder.Entity<UploadContent>()
                .HasMany(uc => uc.Tags)
                .WithMany(t => t.UploadContents)
                .UsingEntity<Dictionary<string, object>>(
                    "TagUploadContent", // Name of the join table
                    j => j.HasOne<Tag>().WithMany().HasForeignKey("TagId"), // Foreign key for Tag
                    j => j.HasOne<UploadContent>().WithMany().HasForeignKey("UploadContentId") // Foreign key for UploadContent
                );

            modelBuilder.Entity<UploadContent>()
                .HasIndex(u => u.Slug)
                .IsUnique();
        }
    }
}
