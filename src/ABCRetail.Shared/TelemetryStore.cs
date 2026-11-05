using Microsoft.Data.SqlClient;

namespace ABCRetail.Shared;

public sealed class TelemetryStore
{
    private const string EnsureSql = """
        IF OBJECT_ID(N'dbo.ProcessedReadings', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.ProcessedReadings
            (
                Id uniqueidentifier NOT NULL CONSTRAINT PK_ProcessedReadings PRIMARY KEY,
                DeviceId nvarchar(64) NOT NULL,
                Metric nvarchar(32) NOT NULL,
                Value float NOT NULL,
                Status nvarchar(16) NOT NULL,
                Reason nvarchar(200) NOT NULL,
                RecordedAtUtc datetime2 NOT NULL,
                ProcessedAtUtc datetime2 NOT NULL
            );
        END
        """;

    private const string InsertSql = """
        IF NOT EXISTS (SELECT 1 FROM dbo.ProcessedReadings WHERE Id = @Id)
        INSERT INTO dbo.ProcessedReadings
            (Id, DeviceId, Metric, Value, Status, Reason, RecordedAtUtc, ProcessedAtUtc)
        VALUES
            (@Id, @DeviceId, @Metric, @Value, @Status, @Reason, @RecordedAtUtc, @ProcessedAtUtc);
        """;

    private const string RecentSql = """
        SELECT TOP (@Take) Id, DeviceId, Metric, Value, Status, Reason, RecordedAtUtc, ProcessedAtUtc
        FROM dbo.ProcessedReadings
        ORDER BY ProcessedAtUtc DESC;
        """;

    private const string SummarySql = """
        SELECT Status, COUNT(*) AS Total
        FROM dbo.ProcessedReadings
        GROUP BY Status;
        """;

    private readonly string _connectionString;

    public TelemetryStore(string connectionString)
    {
        _connectionString = connectionString ?? string.Empty;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(EnsureSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAsync(ProcessedReading reading, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(InsertSql, connection);
        command.Parameters.AddWithValue("@Id", reading.Id);
        command.Parameters.AddWithValue("@DeviceId", Trim(reading.DeviceId, 64));
        command.Parameters.AddWithValue("@Metric", Trim(reading.Metric, 32));
        command.Parameters.AddWithValue("@Value", reading.Value);
        command.Parameters.AddWithValue("@Status", Trim(reading.Status, 16));
        command.Parameters.AddWithValue("@Reason", Trim(reading.Reason, 200));
        command.Parameters.AddWithValue("@RecordedAtUtc", reading.RecordedAtUtc);
        command.Parameters.AddWithValue("@ProcessedAtUtc", reading.ProcessedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessedReading>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take, 1, 200);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(RecentSql, connection);
        command.Parameters.AddWithValue("@Take", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var readings = new List<ProcessedReading>();
        while (await reader.ReadAsync(cancellationToken))
        {
            readings.Add(new ProcessedReading
            {
                Id = reader.GetGuid(0),
                DeviceId = reader.GetString(1),
                Metric = reader.GetString(2),
                Value = reader.GetDouble(3),
                Status = reader.GetString(4),
                Reason = reader.GetString(5),
                RecordedAtUtc = reader.GetDateTime(6),
                ProcessedAtUtc = reader.GetDateTime(7)
            });
        }

        return readings;
    }

    public async Task<ReadingSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SummarySql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts[reader.GetString(0)] = reader.GetInt32(1);
        }

        return new ReadingSummary
        {
            Normal = Count(counts, ReadingStatuses.Normal),
            Warning = Count(counts, ReadingStatuses.Warning),
            Critical = Count(counts, ReadingStatuses.Critical),
            Rejected = Count(counts, ReadingStatuses.Rejected)
        };
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("The TelemetryDb connection string is not configured.");
        }

        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string status) =>
        counts.TryGetValue(status, out var count) ? count : 0;

    private static string Trim(string value, int maxLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
