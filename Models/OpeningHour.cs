namespace rmrLocationsApi.Models;

public class OpeningHour
{
    public Guid Id { get; set; }

    // Navegación anulable para permitir el POST del grafo (ver LocationCategory).
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public bool IsClosed { get; set; }
}

