using MyApp.Modules.Tasks.Domain;
using MyApp.Tests.Modules.Tasks.Support;
using Xunit;

namespace MyApp.Tests.Modules.Tasks.Domain;

// Invariantes de dominio de HU-TASKS-001 (reglas 6 a 8 y 10): no trazan a un CA concreto, por eso
// van en inglés y sin prefijo CA0x_.
public class TaskItemTests
{
    private static readonly DateTime Now = FixedClock.Default;

    [Fact]
    public void Create_GeneratesDistinctIds()
    {
        var first = TaskItem.Create("Comprar pan", null, null, Now);
        var second = TaskItem.Create("Comprar pan", null, null, Now);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_StartsPending_WithEqualUtcTimestamps()
    {
        var task = TaskItem.Create("Comprar pan", null, null, Now);

        Assert.Equal(TaskItemStatus.Pending, task.Status);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Equal(task.CreatedAt, task.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_NonUtcTimestamp_Throws(DateTimeKind kind)
    {
        var notUtc = DateTime.SpecifyKind(Now, kind);

        Assert.Throws<ArgumentException>(() => TaskItem.Create("Comprar pan", null, null, notUtc));
    }

    [Fact]
    public void Create_TodayIsComputedInUtc_WithoutTimeZoneAdjustment()
    {
        // 01:00 UTC del 29 = 20:00 del 28 en UTC-5. Para esa persona "hoy" sería el 28, pero la HU fija
        // "hoy" en UTC (el 29), así que una fecha límite del 28 ya está en el pasado.
        var earlyUtc = new DateTime(2026, 9, 29, 1, 0, 0, DateTimeKind.Utc);

        var exception = Assert.Throws<MyApp.Shared.Kernel.DomainException>(() =>
            TaskItem.Create("Comprar pan", null, "2026-09-28", earlyUtc));

        Assert.Equal(TaskItemErrors.DueDateInPast, exception.Message);
    }

    [Fact]
    public void StatusCode_RoundTrips()
    {
        Assert.Equal("PENDING", TaskItemStatus.Pending.ToCode());
        Assert.Equal(TaskItemStatus.Pending, TaskItemStatusCodes.FromCode("PENDING"));
    }
}
