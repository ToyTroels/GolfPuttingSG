using GolfSG.Server.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SavedRound> SavedRounds => Set<SavedRound>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
