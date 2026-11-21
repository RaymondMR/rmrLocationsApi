using System.ComponentModel.DataAnnotations;

namespace rmrLocationsApi.Models;

public class OpeningHour
{
    public Guid Id { get; set; }

    // Navegación anulable para permitir el POST del grafo (ver LocationCategory).
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    [EnumDataType(typeof(DayOfWeek))] // StringEnumConverter admite números: 99 no debe pasar
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public bool IsClosed { get; set; }
}

