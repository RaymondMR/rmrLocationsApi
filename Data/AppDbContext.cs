using Microsoft.EntityFrameworkCore;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<LocationCategory> LocationCategories => Set<LocationCategory>();
    public DbSet<LocationTag> LocationTags => Set<LocationTag>();
    public DbSet<UserFavoriteLocation> UserFavoriteLocations => Set<UserFavoriteLocation>();
    public DbSet<UserCategoryInterest> UserCategoryInterests => Set<UserCategoryInterest>();
    public DbSet<UserTagFollow> UserTagFollows => Set<UserTagFollow>();

    public DbSet<LocationMedia> LocationMedia => Set<LocationMedia>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<LocationCollection> Collections => Set<LocationCollection>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();
    public DbSet<OpeningHour> OpeningHours => Set<OpeningHour>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.HasPostgresExtension("postgis");
        b.HasPostgresExtension("citext");
        b.HasPostgresExtension("pg_trgm");

        // ------------------------------------------------------------------
        // User
        // ------------------------------------------------------------------
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserName).HasColumnType("citext").HasMaxLength(64).IsRequired();
            e.Property(x => x.Email).HasColumnType("citext").HasMaxLength(256).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.AvatarUrl).HasMaxLength(2048);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.RefreshToken).HasMaxLength(512);
            e.Property(x => x.Roles).HasColumnType("text[]");

            e.HasIndex(x => x.UserName).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        // ------------------------------------------------------------------
        // Location
        // ------------------------------------------------------------------
        b.Entity<Location>(e =>
        {
            e.ToTable("locations", t =>
                t.HasCheckConstraint("ck_locations_rating",
                    "\"AverageRating\" >= 0 AND \"AverageRating\" <= 5"));

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasColumnType("citext").HasMaxLength(220).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.WebsiteUrl).HasMaxLength(2048);
            e.Property(x => x.PhoneNumber).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            // geography(Point,4326): distancias en metros
            e.Property(x => x.Coordinates)
             .HasColumnType("geography (Point, 4326)")
             .IsRequired();

            // Índice espacial GiST para ST_DWithin / KNN
            e.HasIndex(x => x.Coordinates).HasMethod("gist");

            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Status, x.IsPublic });
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops");

            e.OwnsOne(x => x.Address, o =>
            {
                o.Property(p => p.Name).HasColumnName("address_name").HasMaxLength(200);
                o.Property(p => p.Street).HasColumnName("street").HasMaxLength(200);
                o.Property(p => p.ExteriorNumber).HasColumnName("exterior_number").HasMaxLength(20);
                o.Property(p => p.InteriorNumber).HasColumnName("interior_number").HasMaxLength(20);
                o.Property(p => p.Neighborhood).HasColumnName("neighborhood").HasMaxLength(120);
                o.Property(p => p.City).HasColumnName("city").HasMaxLength(120);
                o.Property(p => p.State).HasColumnName("state").HasMaxLength(120);
                o.Property(p => p.PostalCode).HasColumnName("postal_code").HasMaxLength(20);
                o.Property(p => p.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsFixedLength();
            });

            e.HasOne(x => x.Owner)
             .WithMany(u => u.OwnedLocations)
             .HasForeignKey(x => x.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------------------------------------------------------
        // Category
        // ------------------------------------------------------------------
        b.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Slug).HasColumnType("citext").HasMaxLength(140).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.IconName).HasMaxLength(64);
            e.Property(x => x.ColorHex).HasMaxLength(7);

            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.ParentCategoryId, x.SortOrder });

            e.HasOne(x => x.ParentCategory)
             .WithMany(x => x.Children)
             .HasForeignKey(x => x.ParentCategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------------------------------------------------------
        // Tag
        // ------------------------------------------------------------------
        b.Entity<Tag>(e =>
        {
            e.ToTable("tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasColumnType("citext").HasMaxLength(60).IsRequired();
            e.Property(x => x.Slug).HasColumnType("citext").HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);

            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ------------------------------------------------------------------
        // N:M  Location <-> Category
        // ------------------------------------------------------------------
        b.Entity<LocationCategory>(e =>
        {
            e.ToTable("location_categories");
            e.HasKey(x => new { x.LocationId, x.CategoryId });

            e.HasOne(x => x.Location)
             .WithMany(l => l.LocationCategories)
             .HasForeignKey(x => x.LocationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Category)
             .WithMany(c => c.LocationCategories)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.CategoryId);
            e.HasIndex(x => x.LocationId).IsUnique().HasFilter("\"IsPrimary\"");
        });

        // ------------------------------------------------------------------
        // N:M  Location <-> Tag
        // ------------------------------------------------------------------
        b.Entity<LocationTag>(e =>
        {
            e.ToTable("location_tags");
            e.HasKey(x => new { x.LocationId, x.TagId });

            e.HasOne(x => x.Location)
             .WithMany(l => l.LocationTags)
             .HasForeignKey(x => x.LocationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Tag)
             .WithMany(t => t.LocationTags)
             .HasForeignKey(x => x.TagId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.AddedByUser)
             .WithMany()
             .HasForeignKey(x => x.AddedByUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.TagId);
        });

        // ------------------------------------------------------------------
        // N:M con User
        // ------------------------------------------------------------------
        b.Entity<UserFavoriteLocation>(e =>
        {
            e.ToTable("user_favorite_locations");
            e.HasKey(x => new { x.UserId, x.LocationId });
            e.Property(x => x.Notes).HasMaxLength(500);

            e.HasOne(x => x.User)
             .WithMany(u => u.FavoriteLocations)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Location)
             .WithMany(l => l.FavoritedBy)
             .HasForeignKey(x => x.LocationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.LocationId);
        });

        b.Entity<UserCategoryInterest>(e =>
        {
            e.ToTable("user_category_interests");
            e.HasKey(x => new { x.UserId, x.CategoryId });

            e.HasOne(x => x.User)
             .WithMany(u => u.CategoryInterests)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Category)
             .WithMany(c => c.InterestedUsers)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.CategoryId);
        });

        b.Entity<UserTagFollow>(e =>
        {
            e.ToTable("user_tag_follows");
            e.HasKey(x => new { x.UserId, x.TagId });

            e.HasOne(x => x.User)
             .WithMany(u => u.FollowedTags)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Tag)
             .WithMany(t => t.Followers)
             .HasForeignKey(x => x.TagId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.TagId);
        });

        // ------------------------------------------------------------------
        // LocationMedia
        // ------------------------------------------------------------------
        b.Entity<LocationMedia>(e =>
        {
            e.ToTable("location_media");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            e.Property(x => x.ThumbnailUrl).HasMaxLength(2048);
            e.Property(x => x.Caption).HasMaxLength(300);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);

            e.HasOne(x => x.Location)
             .WithMany(l => l.Media)
             .HasForeignKey(x => x.LocationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.UploadedByUser)
             .WithMany()
             .HasForeignKey(x => x.UploadedByUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.LocationId, x.SortOrder });
            e.HasIndex(x => x.LocationId).IsUnique().HasFilter("\"IsCover\" AND NOT \"IsDeleted\"");
        });

        // ------------------------------------------------------------------
        // Review
        // ------------------------------------------------------------------
        b.Entity<Review>(e =>
        {
            e.ToTable("reviews", t =>
                t.HasCheckConstraint("ck_reviews_rating", "\"Rating\" BETWEEN 1 AND 5"));

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(4000);

            e.HasOne(x => x.Location)
             .WithMany(l => l.Reviews)
             .HasForeignKey(x => x.LocationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
             .WithMany(u => u.Reviews)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.LocationId, x.UserId })
             .IsUnique()
             .HasFilter("NOT \"IsDeleted\"");
        });

        // ------------------------------------------------------------------
        // Collections
        // ------------------------------------------------------------------
        b.Entity<LocationCollection>(e =>
        {
            e.ToTable("collections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);

            e.HasOne(x => x.Owner)
             .WithMany(u => u.Collections)
             .HasForeignKey(x => x.OwnerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique().HasFilter("NOT \"IsDeleted\"");
        });

        b.Entity<CollectionItem>(e =>
        {
            e.ToTable("collection_items");
            e.HasKey(x => new { x.CollectionId, x.LocationId });
            e.Property(x => x.Notes).HasMaxLength(500);

            e.HasOne(x => x.Collection)
             .WithMany(c => c.Items)
             .HasForeignKey(x => x.CollectionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Location)
             .WithMany(l => l.CollectionItems)
             .HasForeignKey(x => x.LocationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.LocationId);
        });

        b.Entity<OpeningHour>(e =>
        {
            e.ToTable("opening_hours");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

            e.HasOne(x => x.Location)
             .WithMany(l => l.OpeningHours)
             .HasForeignKey(x => x.LocationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.LocationId, x.DayOfWeek });
        });

        // ------------------------------------------------------------------
        // Convenciones globales
        // ------------------------------------------------------------------
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            var version = entityType.FindProperty(nameof(AuditableEntity.Version));
            if (version is not null)
            {
                version.SetColumnName("xmin");
                version.SetColumnType("xid");
                version.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate;
                version.IsConcurrencyToken = true;
            }
        }

        // Soft delete: filtro global en los agregados raíz
        b.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Location>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Category>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Tag>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Review>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<LocationMedia>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<LocationCollection>().HasQueryFilter(x => !x.IsDeleted);
    }

    public override int SaveChanges()
        => SaveChangesAsync().GetAwaiter().GetResult();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAtUtc = now;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
