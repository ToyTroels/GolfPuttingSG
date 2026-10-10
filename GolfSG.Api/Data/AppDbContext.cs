using GolfSG.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

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
    }
}
