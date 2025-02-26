namespace Delta;

public static partial class DeltaExtensions
{
    #region DiscoverConnection

    static (Type sqlConnection, Type transaction) FindConnectionType()
    {
        var sqlConnection = Type.GetType("Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient");
        if (sqlConnection != null)
        {
            var transaction = sqlConnection.Assembly.GetType("Microsoft.Data.SqlClient.SqlTransaction")!;
            return (sqlConnection, transaction);
        }

        var npgsqlConnection = Type.GetType("Npgsql.NpgsqlConnection, Npgsql");
        if (npgsqlConnection != null)
        {
            var transaction = npgsqlConnection.Assembly.GetType("Npgsql.NpgsqlTransaction")!;
            return (npgsqlConnection, transaction);
        }

        var mySqlConnection = Type.GetType("MySqlConnector.MySqlConnection, MySqlConnector");
        if (mySqlConnection != null)
        {
            var transaction = mySqlConnection.Assembly.GetType("MySqlConnector.MySqlTransaction")!;
            return (mySqlConnection, transaction);
        }

        throw new("Could not find connection type. Tried SqlConnection, NpgsqlConnection and MySqlConnection");
    }

    static Connection DiscoverConnection(HttpContext httpContext)
    {
        var (connectionType, transactionType) = FindConnectionType();
        var provider = httpContext.RequestServices;
        var connection = (DbConnection) provider.GetRequiredService(connectionType);
        var transaction = (DbTransaction?) provider.GetService(transactionType);
        return new(connection, transaction);
    }

    #endregion
}