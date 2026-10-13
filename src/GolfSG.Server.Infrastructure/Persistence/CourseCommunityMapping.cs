using GolfSG.Server.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

internal static class CourseCommunityMapping
{
    internal static void Configure(ModelBuilder model)
    {
        var image = model.Entity<SharedCourseImage>();
        image.ToTable("course_images"); image.HasKey(x => x.Id);
        image.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        image.Property(x => x.Bytes).HasColumnName("bytes").IsRequired();

        var course = model.Entity<SharedCourse>();
        course.ToTable("shared_courses"); course.HasKey(x => x.Id);
        course.Property(x => x.Id).HasColumnName("id");
        course.Property(x => x.OwnerId).HasColumnName("owner_id");
        course.Property(x => x.Creator).HasColumnName("creator");
        course.Property(x => x.SourceId).HasColumnName("source_id").HasMaxLength(100);
        course.Property(x => x.RevisionId).HasColumnName("revision_id");
        course.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        course.Property(x => x.Positions).HasColumnName("positions");
        course.Property(x => x.Attempts).HasColumnName("attempts");
        course.Property(x => x.Published).HasColumnName("published");
        course.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        course.HasIndex(x => new { x.OwnerId, x.SourceId }).IsUnique();

        var revision = model.Entity<SharedCourseRevision>();
        revision.ToTable("course_revisions"); revision.HasKey(x => x.Id);
        revision.Property(x => x.Id).HasColumnName("id");
        revision.Property(x => x.CourseId).HasColumnName("course_id");
        revision.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        revision.Property(x => x.DefinitionJson).HasColumnName("definition").HasColumnType("jsonb");
        revision.Property(x => x.ImageId).HasColumnName("image_id").HasMaxLength(64);
        revision.HasOne<SharedCourse>().WithMany().HasForeignKey(x => x.CourseId);
        revision.HasOne<SharedCourseImage>().WithMany().HasForeignKey(x => x.ImageId);
        revision.HasIndex(x => new { x.CourseId, x.ContentHash }).IsUnique();

        var game = model.Entity<SharedCourseGame>();
        game.ToTable("course_games"); game.HasKey(x => x.Id);
        game.Property(x => x.Id).HasColumnName("id");
        game.Property(x => x.HostId).HasColumnName("host_id");
        game.Property(x => x.HostName).HasColumnName("host_name");
        game.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        game.Property(x => x.Code).HasColumnName("code").HasMaxLength(6);
        game.Property(x => x.DefinitionJson).HasColumnName("definition").HasColumnType("jsonb");
        game.Property(x => x.ImageId).HasColumnName("image_id").HasMaxLength(64);
        game.Property(x => x.ScoringVersion).HasColumnName("scoring_version");
        game.Property(x => x.Closed).HasColumnName("closed");
        game.Property(x => x.CreatedAt).HasColumnName("created_at");
        game.HasIndex(x => x.Code).IsUnique();
        game.HasOne<SharedCourseImage>().WithMany().HasForeignKey(x => x.ImageId);

        var player = model.Entity<SharedGameParticipant>();
        player.ToTable("course_game_participants"); player.HasKey(x => new { x.GameId, x.AccountId });
        player.Property(x => x.GameId).HasColumnName("game_id");
        player.Property(x => x.AccountId).HasColumnName("account_id");
        player.Property(x => x.Name).HasColumnName("name");
        player.Property(x => x.RoundId).HasColumnName("round_id").HasMaxLength(32);
        player.Property(x => x.ResultsJson).HasColumnName("results").HasColumnType("jsonb");
        player.Property(x => x.TotalStrokesGained).HasColumnName("total_sg");
        player.HasOne<SharedCourseGame>().WithMany().HasForeignKey(x => x.GameId);
        player.HasIndex(x => x.AccountId);
    }
}
