using Microsoft.EntityFrameworkCore;
using Mostlylucid.DbContext.EntityFramework.Converters;
using Mostlylucid.Shared.Entities;

namespace Mostlylucid.DbContext.EntityFramework;

public class MostlylucidDbContext : Microsoft.EntityFrameworkCore.DbContext, IMostlylucidDBContext
{
    public MostlylucidDbContext(DbContextOptions<MostlylucidDbContext> contextOptions) : base(contextOptions)
    {
    }

    public DbSet<CommentEntity> Comments { get; set; }
    
    public DbSet<CommentClosure> CommentClosures { get; set; }
    public DbSet<BlogPostEntity> BlogPosts { get; set; }
    public DbSet<CategoryEntity> Categories { get; set; }

    public DbSet<LanguageEntity> Languages { get; set; }
    
    public DbSet<EmailSubscriptionSendLogEntity> EmailSubscriptionSendLogs { get; set; }

    public DbSet<EmailSubscriptionEntity> EmailSubscriptions { get; set; }

    public DbSet<MarkdownFetchEntity> MarkdownFetches { get; set; }

    public DbSet<DownloadedImageEntity> DownloadedImages { get; set; }

    public DbSet<WorkflowDefinitionEntity> WorkflowDefinitions { get; set; }

    public DbSet<WorkflowExecutionEntity> WorkflowExecutions { get; set; }

    public DbSet<WorkflowTriggerStateEntity> WorkflowTriggerStates { get; set; }


    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("mostlylucid");
        
        modelBuilder.Entity<EmailSubscriptionSendLogEntity>(entity =>
        {
            entity.HasKey(x => x.SubscriptionType);
            entity.Property(x => x.LastSent).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        modelBuilder.Entity<EmailSubscriptionEntity>(entity =>
            {
                entity.ToTable("EmailSubscriptions");
                entity.HasMany(b => b.Categories)
                    .WithMany(c => c.EmailSubscriptions)
                    .UsingEntity<Dictionary<string, object>>(
                        "EmailSubscription_Category",
                        b => b.HasOne<CategoryEntity>().WithMany().OnDelete(DeleteBehavior.NoAction)
                            .HasForeignKey("CategoryId"),
                        c => c.HasOne<EmailSubscriptionEntity>().WithMany().OnDelete(DeleteBehavior.NoAction)
                            .HasForeignKey("EmailSubscriptionId")
                    );
                
                entity.HasIndex(x => x.Token).IsUnique();
                entity.HasIndex(x => x.Email);
            }
        );

        modelBuilder.Entity<MarkdownFetchEntity>(entity =>
        {
            entity.ToTable("MarkdownFetches");
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.BlogPost)
                .WithMany()
                .HasForeignKey(x => x.BlogPostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.Url, x.BlogPostId }).IsUnique();
            entity.HasIndex(x => x.LastFetchedAt);
            entity.HasIndex(x => x.IsEnabled);

            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<DownloadedImageEntity>(entity =>
        {
            entity.ToTable("DownloadedImages");
            entity.HasKey(x => x.Id);

            // Unique constraint on original URL per post slug
            entity.HasIndex(x => new { x.PostSlug, x.OriginalUrl }).IsUnique();

            // Index for finding images by post slug
            entity.HasIndex(x => x.PostSlug);

            // Index for finding images by original URL
            entity.HasIndex(x => x.OriginalUrl);

            // Index for cleanup queries (old unverified images)
            entity.HasIndex(x => x.LastVerifiedDate);

            entity.Property(x => x.DownloadedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.LastVerifiedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });


    modelBuilder.Entity<BlogPostEntity>(entity =>
        {
            entity.HasIndex(x => new { x.Slug, x.LanguageId });
            entity.HasIndex(x => x.ContentHash).IsUnique();
            entity.HasIndex(x => x.PublishedDate);

            // Indexes for new visibility features
            entity.HasIndex(x => new { x.IsHidden, x.ScheduledPublishDate }); // For filtering in WHERE clauses
            entity.HasIndex(x => new { x.IsPinned, x.PublishedDate }); // For ordering on page 1

            entity.Property(b=>b.UpdatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(b => b.SearchVector)
                .HasComputedColumnSql("to_tsvector('english', coalesce(\"Title\", '') || ' ' || coalesce(\"PlainTextContent\", ''))", stored: true);
            
           entity.HasIndex(b => b.SearchVector)
                .HasMethod("GIN");
           // Configure the CommentClosure entity
           modelBuilder.Entity<CommentClosure>()
               .HasKey(cc => new { cc.AncestorId, cc.DescendantId });

           modelBuilder.Entity<CommentClosure>()
               .HasOne(cc => cc.Ancestor)
               .WithMany(c => c.Descendants)
               .HasForeignKey(cc => cc.AncestorId)
               .OnDelete(DeleteBehavior.Restrict);

           modelBuilder.Entity<CommentClosure>()
               .HasOne(cc => cc.Descendant)
               .WithMany(c => c.Ancestors)
               .HasForeignKey(cc => cc.DescendantId)
               .OnDelete(DeleteBehavior.Cascade);
           
           modelBuilder.Entity<CommentEntity>(entity =>
           {
               entity.HasKey(c => c.Id);  // Primary key

               entity.Property(c => c.Content)
                   .IsRequired()
                   .HasMaxLength(1000);  // Example constraint on content length

               entity.Property(x=>x.CreatedAt)
                   .HasDefaultValueSql("CURRENT_TIMESTAMP");
               entity.Property(x=>x.Author).IsRequired().HasMaxLength(200);
               entity.Property(c => c.CreatedAt)
                   .IsRequired();  // Ensure the creation date is required

               // Configure the relationship between Comment and Post (optional)
               entity.HasOne(c => c.Post)
                   .WithMany(p => p.Comments)
                   .HasForeignKey(c => c.PostId)
                   .OnDelete(DeleteBehavior.Cascade);  // Optional, cascade delete on post deletion

               entity.HasIndex(c => c.Author);
           });

            entity.HasOne(b => b.LanguageEntity)
                .WithMany(l => l.BlogPosts).HasForeignKey(x => x.LanguageId);

            entity.HasMany(b => b.Categories)
                .WithMany(c => c.BlogPosts)
                .UsingEntity<Dictionary<string, object>>(
                    "blogpostcategory",
                    c => c.HasOne<CategoryEntity>().WithMany().HasForeignKey("CategoryId"),
                    b => b.HasOne<BlogPostEntity>().WithMany().HasForeignKey("BlogPostId")
                );
        });

     
        modelBuilder.Entity<LanguageEntity>(entity =>
        {
            entity.HasMany(l => l.BlogPosts)
                .WithOne(b => b.LanguageEntity);
        });

        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.HasIndex(b => b.Name).HasMethod("GIN").IsTsVectorExpressionIndex("english");;
            entity.HasKey(c => c.Id); // Assuming Category has a primary key named Id

            entity.HasMany(c => c.BlogPosts)
                .WithMany(b => b.Categories)
                .UsingEntity<Dictionary<string, object>>(
                    "blogpostcategory",
                    b => b.HasOne<BlogPostEntity>().WithMany().HasForeignKey("BlogPostId"),
                    c => c.HasOne<CategoryEntity>().WithMany().HasForeignKey("CategoryId")
                );
        });

        // Workflow entities configuration
        modelBuilder.Entity<WorkflowDefinitionEntity>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.HasIndex(w => w.WorkflowId).IsUnique();
            entity.HasIndex(w => w.Name);
            entity.HasIndex(w => w.IsEnabled);
            entity.Property(w => w.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(w => w.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasMany(w => w.Executions)
                .WithOne(e => e.WorkflowDefinition)
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowExecutionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExecutionId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.Status });
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<WorkflowTriggerStateEntity>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => new { t.WorkflowDefinitionId, t.TriggerType });
            entity.HasIndex(t => t.IsEnabled);
            entity.HasIndex(t => t.LastCheckedAt);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(t => t.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(t => t.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(t => t.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}