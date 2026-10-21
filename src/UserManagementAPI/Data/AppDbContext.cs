
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Models;

namespace UserManagementAPI.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(254);

            entity.Property(u => u.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(254);

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique();

            entity.Property(u => u.Department)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.CreatedAtUtc)
                .IsRequired();

            entity.Property(u => u.UpdatedAtUtc)
                .IsRequired();
        });
    }

    public override int SaveChanges()
    {
        PrepareUserEntities();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        PrepareUserEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void PrepareUserEntities()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<User>())
        {
            if (entry.State != EntityState.Added &&
                entry.State != EntityState.Modified)
            {
                continue;
            }

            // Normalize the email before saving.
            if (string.IsNullOrWhiteSpace(entry.Entity.Email))
            {
                throw new InvalidOperationException(
                    "User email cannot be empty.");
            }

            entry.Entity.Email = entry.Entity.Email.Trim();

            entry.Entity.NormalizedEmail =
                entry.Entity.Email.ToUpperInvariant();

            if (entry.State == EntityState.Added)
            {
                // New user: set both timestamps.
                entry.Entity.CreatedAtUtc = now;
            }
            else
            {
                // Existing user: protect the original creation date.
                entry.Property(u => u.CreatedAtUtc)
                    .IsModified = false;
            }

            // New or modified user.
            entry.Entity.UpdatedAtUtc = now;
        }
    }
}