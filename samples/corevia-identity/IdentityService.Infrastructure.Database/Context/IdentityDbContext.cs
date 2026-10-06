using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Database.Context;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientSecret> ClientSecrets => Set<ClientSecret>();
    public DbSet<ClientScope> ClientScopes => Set<ClientScope>();

    public DbSet<Scope> Scopes => Set<Scope>();
    public DbSet<ScopePermission> ScopePermissions => Set<ScopePermission>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PhoneNumber)
                .IsRequired();

            builder.HasIndex(x => x.PhoneNumber)
                .IsUnique();

            builder.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<UserProfile>(builder =>
        {
            builder.HasKey(x => x.UserId);

            builder.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<UserSession>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<UserRole>(builder =>
        {
            builder.HasKey(x => new { x.UserId, x.RoleId });
        });

        modelBuilder.Entity<UserTenant>(builder =>
        {
            builder.HasKey(x => new { x.UserId, x.TenantId });
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired();

            builder.Property(x => x.DisplayName)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.Name)
                .IsUnique();
        });

        modelBuilder.Entity<Permission>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Key)
                .IsRequired();

            builder.Property(x => x.DisplayName)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.Key)
                .IsUnique();
        });

        modelBuilder.Entity<RolePermission>(builder =>
        {
            builder.HasKey(x => new { x.RoleId, x.PermissionId });
        });

        modelBuilder.Entity<Client>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClientId)
                .IsRequired();

            builder.Property(x => x.Name)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.ClientId)
                .IsUnique();
        });

        modelBuilder.Entity<ClientSecret>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Hash)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<ClientScope>(builder =>
        {
            builder.HasKey(x => new { x.ClientId, x.ScopeId });
        });

        modelBuilder.Entity<Scope>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired();

            builder.Property(x => x.DisplayName)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.Name)
                .IsUnique();
        });

        modelBuilder.Entity<ScopePermission>(builder =>
        {
            builder.HasKey(x => new { x.ScopeId, x.PermissionId });
        });

        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired();

            builder.Property(x => x.DisplayName)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Token)
                .IsRequired();

            builder.HasIndex(x => x.Token)
                .IsUnique();

            builder.Property(x => x.ExpiresAt)
                .IsRequired(false);

            builder.Property(x => x.CreatedAt)
                .IsRequired();
        });
    }
}
