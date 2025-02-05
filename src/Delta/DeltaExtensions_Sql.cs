// ReSharper disable UseRawString

using System.Globalization;

namespace Delta;

public static partial class DeltaExtensions
{
    internal static Task<string> GetLastTimeStamp(DbConnection connection, DbTransaction? transaction = null, Cancel cancel = default) =>
        ExecuteCommand(connection, transaction, ExecuteTimestampQuery, cancel);

    static async Task<string> ExecuteCommand(DbConnection connection, DbTransaction? transaction, Func<DbCommand, Cancel, Task<string>> execute, Cancel cancel)
    {
        await using var command = connection.CreateCommand();
        if (transaction != null)
        {
            command.Transaction = transaction;
        }

        if (connection.State != ConnectionState.Closed)
        {
            return await execute(command, cancel);
        }

        await connection.OpenAsync(cancel);
        try
        {
            return await execute(command, cancel);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public static async Task<string> GetLastTimeStamp(this DbConnection connection, Cancel cancel = default)
    {
        await using var command = connection.CreateCommand();
        return await ExecuteTimestampQuery(command, cancel);
    }

    static async Task<string> ExecuteTimestampQuery(DbCommand command, Cancel cancel = default)
    {
        var name = command.GetType().Name;
        if (name == "SqlCommand")
        {
            command.CommandText = $@"
-- begin-snippet: SqlServerTimestamp
select top 1 [End Time]
from fn_dblog(null, null)
where [Operation] = 'LOP_COMMIT_XACT'
order by [End Time] desc;
-- end-snippet
";

            var startNew = Stopwatch.StartNew();
            await using var reader = await command.ExecuteReaderAsync(cancel);
            var readAsync = await reader.ReadAsync(cancel);
            // no results on first run
            if(!readAsync)
            {
                return string.Empty;
            }

            var executeTimestampQuery = await reader.GetFieldValueAsync<string>(0, cancel);
            startNew.Stop();
            return executeTimestampQuery;
        }

        if (name == "NpgsqlCommand")
        {
            command.CommandText = @"
-- begin-snippet: PostgresTimestamp
select pg_last_committed_xact();
-- end-snippet
";
            var result = (object[]?) await command.ExecuteScalarAsync(cancel);
            // null on first run after SET track_commit_timestamp to 'on'
            if (result is null)
            {
                return string.Empty;
            }

            var xid = (uint) result[0];
            return xid.ToString();
        }

        throw new("Unsupported type " + name);
    }
}