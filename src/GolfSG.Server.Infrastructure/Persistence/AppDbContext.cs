using GolfSG.Server.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SavedRound> SavedRounds => Set<SavedRound>();
    public DbSet<SharedCourseImage> CourseImages => Set<SharedCourseImage>();
    public DbSet<SharedCourse> SharedCourses => Set<SharedCourse>();
    public DbSet<SharedCourseRevision> CourseRevisions => Set<SharedCourseRevision>();
    public DbSet<SharedCourseGame> CourseGames => Set<SharedCourseGame>();
    public DbSet<SharedGameParticipant> GameParticipants => Set<SharedGameParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        CourseCommunityMapping.Configure(modelBuilder);

        var product = modelBuilder.Entity<Product>();
        product.ToTable("products");
        product.HasKey(p => p.Id);
        // Match the existing SERIAL column and its sequence-backed default.
        product.Property(p => p.Id).HasColumnName("id").UseSerialColumn();
        product.Property(p => p.Name).HasColumnName("name")
            .HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();

        var round = modelBuilder.Entity<SavedRound>();
        round.ToTable("saved_rounds");
        round.HasKey(r => new { r.UserId, r.Id });
        round.Property(r => r.UserId).HasColumnName("user_id").HasColumnType("text").IsRequired();
        round.Property(r => r.Id).HasColumnName("id").HasMaxLength(100);
        round.Property(r => r.Version).HasColumnName("version");
        round.Property(r => r.Payload).HasColumnName("payload").HasColumnType("jsonb");
        round.Property(r => r.UpdatedAt).HasColumnName("updated_at");
    }
}
