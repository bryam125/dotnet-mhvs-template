using Microsoft.Extensions.Configuration;
using MyApp.Modules.Tasks.Domain;
using MySqlConnector;

namespace MyApp.Modules.Tasks.Infrastructure;

/// <summary>
/// Adaptador del puerto <see cref="TaskRepository"/> contra MySQL, con ADO.NET puro (MySqlConnector),
/// sin ORM. La tabla la describe <c>schema.sql</c>, junto a este archivo; la app no la crea ni la migra.
/// </summary>
/// <remarks>
/// <para>SQL siempre parametrizado: ningún valor de la tarea se concatena en el texto del comando.</para>
/// <para>
/// Fechas: las columnas son <c>DATETIME(6)</c>, que MySQL guarda tal cual, sin convertir por la zona
/// horaria de la sesión (a diferencia de <c>TIMESTAMP</c>). Se escribe siempre un instante UTC y al
/// leer se marca explícitamente como <see cref="DateTimeKind.Utc"/>, sin depender de opciones de la
/// cadena de conexión.
/// </para>
/// </remarks>
public sealed class MySqlTaskRepository(IConfiguration configuration) : TaskRepository
{
    public const string ConnectionStringName = "MysqlTodo";

    private const string InsertSql = """
        INSERT INTO tasks (id, title, description, due_date, status, created_at, updated_at)
        VALUES (@id, @title, @description, @due_date, @status, @created_at, @updated_at);
        """;

    private const string SelectByIdSql = """
        SELECT id, title, description, due_date, status, created_at, updated_at
        FROM tasks
        WHERE id = @id;
        """;

    public async Task AddAsync(TaskItem task, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new MySqlCommand(InsertSql, connection);

        command.Parameters.Add("@id", MySqlDbType.VarChar).Value = task.Id.ToString("D");
        command.Parameters.Add("@title", MySqlDbType.VarChar).Value = task.Title;
        command.Parameters.Add("@description", MySqlDbType.VarChar).Value = (object?)task.Description ?? DBNull.Value;
        command.Parameters.Add("@due_date", MySqlDbType.Date).Value = task.DueDate is { } dueDate ? dueDate : DBNull.Value;
        command.Parameters.Add("@status", MySqlDbType.VarChar).Value = task.Status.ToCode();
        command.Parameters.Add("@created_at", MySqlDbType.DateTime).Value = ToStorageUtc(task.CreatedAt);
        command.Parameters.Add("@updated_at", MySqlDbType.DateTime).Value = ToStorageUtc(task.UpdatedAt);

        // Un único INSERT es atómico: la fila queda completa o no queda (regla 9).
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new MySqlCommand(SelectByIdSql, connection);
        command.Parameters.Add("@id", MySqlDbType.VarChar).Value = id.ToString("D");

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return TaskItem.Rehydrate(
            id: ReadGuid(reader, 0),
            title: reader.GetString(1),
            description: reader.IsDBNull(2) ? null : reader.GetString(2),
            dueDate: reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3),
            status: TaskItemStatusCodes.FromCode(reader.GetString(4)),
            createdAt: FromStorageUtc(reader.GetDateTime(5)),
            updatedAt: FromStorageUtc(reader.GetDateTime(6)));
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken ct)
    {
        // Se lee en cada operación (no en el constructor): así la app arranca sin la cadena configurada
        // y el error aparece al usarla, registrado por el handler con el id de la tarea.
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Falta la cadena de conexión ConnectionStrings:{ConnectionStringName} " +
                "(config/connectionstrings.local.json o la variable de entorno ConnectionStrings__MysqlTodo).");

        var connection = new MySqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static DateTime ToStorageUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Solo se guardan instantes en UTC.", nameof(value));

        // DATETIME(6) guarda microsegundos; se truncan aquí los 100 ns sobrantes de .NET para que el
        // valor guardado no dependa de si el servidor redondea o trunca.
        return new DateTime(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
    }

    private static DateTime FromStorageUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // Con las opciones por defecto MySqlConnector devuelve una columna CHAR(36) como Guid; con
    // GuidFormat=None la devuelve como texto. Se aceptan ambas para no depender de la cadena de conexión.
    private static Guid ReadGuid(MySqlDataReader reader, int ordinal) =>
        reader.GetValue(ordinal) switch
        {
            Guid guid => guid,
            string text => Guid.Parse(text),
            var other => throw new InvalidCastException($"Valor inesperado para tasks.id: {other.GetType().Name}.")
        };
}
