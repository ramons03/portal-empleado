using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Domain.Entities;

namespace SAED_PortalEmpleado.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; }
    public DbSet<ReciboViewEvent> ReciboViewEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GoogleSub).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Cuil).HasMaxLength(32);
            entity.Property(e => e.PictureUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasIndex(e => e.GoogleSub).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Cuil).IsUnique();
        });

        modelBuilder.Entity<ReciboViewEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GoogleSub).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Cuil).HasMaxLength(32);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ReciboId).HasMaxLength(128);
            entity.Property(e => e.ViewedAtUtc).IsRequired();

            entity.HasIndex(e => e.GoogleSub);
            entity.HasIndex(e => e.Cuil);
            entity.HasIndex(e => e.ViewedAtUtc);
        });
    }
}
