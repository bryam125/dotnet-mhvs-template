using MyApp.Shared.Ports;

namespace MyApp.Tests.Modules.Tasks.Support;

/// <summary>Reloj fijo para que "hoy (UTC)" sea determinista en los tests.</summary>
internal sealed class FixedClock(DateTime utcNow) : IClock
{
    public static readonly DateTime Default = new(2026, 9, 28, 15, 30, 45, DateTimeKind.Utc);

    public FixedClock() : this(Default) { }

    public DateTime UtcNow { get; } = utcNow;

    public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
}
