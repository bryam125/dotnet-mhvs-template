using System.Net;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Cableado HTTP de <c>POST /tasks</c> (HU-TASKS-001) contra la app real: el endpoint existe, el campo
/// JSON <c>dueDate</c> se enlaza y el filtro de validación responde 400 antes de llegar al repositorio.
/// </summary>
/// <remarks>
/// Solo casos inválidos a propósito: nunca llegan a MySQL. Un caso válido intentaría escribir en la base
/// configurada en <c>config/connectionstrings.local.json</c>, que en una máquina de desarrollo es real.
/// </remarks>
[Collection(BootstrapCollection.Name)]
public class TasksEndpointsTests(BootstrapFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task PostTasks_BlankTitle_Returns400_WithTitleError()
    {
        var response = await client.PostAsync("/tasks", HttpAssertions.Json("""{"title":"   "}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await HttpAssertions.ReadJsonAsync(response);
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal("El título es obligatorio y debe contener texto válido.", errors.GetProperty("Title")[0].GetString());
    }

    [Fact]
    public async Task PostTasks_DueDateWithTime_Returns400_WithFormatError()
    {
        var response = await client.PostAsync("/tasks", HttpAssertions.Json("""{"title":"Comprar pan","dueDate":"2026-10-01T10:00:00"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await HttpAssertions.ReadJsonAsync(response);
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal("La fecha límite debe tener el formato AAAA-MM-DD.", errors.GetProperty("DueDate")[0].GetString());
    }
}
