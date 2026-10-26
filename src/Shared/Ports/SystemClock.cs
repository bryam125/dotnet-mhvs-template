namespace MyApp.Shared.Ports;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
