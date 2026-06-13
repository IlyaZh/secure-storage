using Microsoft.EntityFrameworkCore;
using SecureStorage.Domain.Entities;

namespace SecureStorage.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Invite> Invites { get; set; }
    public DbSet<Secret> Secrets { get; set; }
    public DbSet<UserQuota> UserQuota { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_unicode_ci");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasConversion(v => v.ToLower().Trim(), v => v);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Id).IsUnique();

            entity.HasOne(e => e.Quota).WithOne().HasForeignKey<UserQuota>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Invite>(entity =>
        {
            entity.ToTable("invites");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasConversion(v => v.ToLower().Trim(), v => v);
            entity.Property(e => e.IsUsed).HasDefaultValue(false);
            entity.Property(e => e.IssuedByUserId).IsRequired();
            entity.Property(e => e.UsedAt).IsRequired(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasIndex(e => e.Id).IsUnique();
            entity.HasIndex(e => e.Email);

        });

        modelBuilder.Entity<Secret>(entity =>
        {
            entity.ToTable("secrets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerId).IsRequired();
            entity.Property(e => e.Comment).IsRequired();
            entity.Property(e => e.IsOneTime).IsRequired();
            entity.Property(e => e.IsBurned).IsRequired();
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(130);
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.IV).IsRequired();
            entity.Property(e => e.Size).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.Property(e => e.ExpiresAt).IsRequired();

            entity.HasOne(e => e.Owner).WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Id).IsUnique();
            entity.HasIndex(e => e.ExpiresAt).IsDescending();
            entity.HasIndex(e => e.OwnerId);
        });

        modelBuilder.Entity<UserQuota>(entity =>
        {
            entity.ToTable("user_quota");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Quota).IsRequired().HasColumnName("quota_bytes");
            entity.Property(e => e.UsedQuota).IsRequired().HasColumnName("used_quota_bytes");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasIndex(e => e.UserId).IsUnique();
        });
    }
}
