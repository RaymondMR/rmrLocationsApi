namespace rmrLocationsApi.Models;

public abstract class AuditableEntity : ISoftDeletable
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Concurrencia optimista en PostgreSQL: se mapea a la columna de sistema
    /// <c>xmin</c>. Es de solo lectura, nunca la asignes manualmente.
    /// </summary>
    public uint Version { get; set; }
}

