# <img src="/src/icon.png" height="30px"> Delta

[![Build status](https://ci.appveyor.com/api/projects/status/20t96gnsmysklh09/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/Delta)
[![NuGet Status](https://img.shields.io/nuget/v/Delta.svg)](https://www.nuget.org/packages/Delta/)

Delta is an opinionated approach to implementing a [304 Not Modified](https://www.keycdn.com/support/304-not-modified)

The approach uses a last updated timestamp from the database to generate an [ETag](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/ETag). All dynamic requests then have that ETag checked/applied.

This approach works well when the frequency of updates is relatively low. In this scenario, the majory of requests will leverage the result in a 304 Not Modified being returned and the browser loading the content its cache.

Effectively consumers will always receive the most current data, while the load on the server remains very low.

**See [Milestones](../../milestones?state=closed) for release notes.**


## Assumptions

Assumes the following combination of technologies are being used:

 * Frequency of updates to data is relatively low compared to reads
 * [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/)
 * [Microsoft SQL Server](https://www.microsoft.com/en-au/sql-server)
 * Either [SQL Server Change Tracking](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/track-data-changes-sql-server) and/or [SQL Server Row Versioning](https://learn.microsoft.com/en-us/sql/t-sql/data-types/rowversion-transact-sql)


## 304 Not Modified Flow

```mermaid
graph TD
    Request
    CalculateEtag[Calculate current ETag<br/>based on timestamp<br/>from web assembly and SQL]
    IfNoneMatch{Has<br/>If-None-Match<br/>header?}
    EtagMatch{Current<br/>Etag matches<br/>If-None-Match?}
    AddETag[Add current ETag<br/>to Response headers]
    304[Respond with<br/>304 Not-Modified]
    Request --> CalculateEtag
    CalculateEtag --> IfNoneMatch
    IfNoneMatch -->|Yes| EtagMatch
    IfNoneMatch -->|No| AddETag
    EtagMatch -->|No| AddETag
    EtagMatch -->|Yes| 304
```


## ETag calculation logic

The ETag is calculated from a combination several parts


#### AssemblyWriteTime

The last write time of the web entry point assembly

<!-- snippet: AssemblyWriteTime -->
<a id='snippet-AssemblyWriteTime'></a>
```cs
var webAssemblyLocation = Assembly.GetEntryAssembly()!.Location;
AssemblyWriteTime = File.GetLastWriteTime(webAssemblyLocation).Ticks.ToString();
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L229-L234' title='Snippet source file'>snippet source</a> | <a href='#snippet-AssemblyWriteTime' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### SQL timestamp

A combination of [change_tracking_current_version](https://learn.microsoft.com/en-us/sql/relational-databases/system-functions/change-tracking-current-version-transact-sql) (if tracking is enabled) and [@@DBTS (row version timestamp)](https://learn.microsoft.com/en-us/sql/t-sql/functions/dbts-transact-sql)


<!-- snippet: SqlTimestamp -->
<a id='snippet-SqlTimestamp'></a>
```cs
declare @changeTracking bigint = change_tracking_current_version();
declare @timeStamp bigint = convert(bigint, @@dbts);

if (@changeTracking is null)
  select cast(@timeStamp as varchar)
else
  select cast(@timeStamp as varchar) + '-' + cast(@changeTracking as varchar)
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L213-L221' title='Snippet source file'>snippet source</a> | <a href='#snippet-SqlTimestamp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Suffix

An optional string suffix that is dynamically caculated at runtime based on the current `HttpContext`.

<!-- snippet: Suffix -->
<a id='snippet-Suffix'></a>
```cs
var app = builder.Build();
app.UseDelta(
    getConnection: httpContext => httpContext.RequestServices.GetRequiredService<SqlConnection>(),
    suffix: httpContext => "MySuffix");
```
<sup><a href='/src/DeltaTests/Usage.cs#L9-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-Suffix' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Combining the above

<!-- snippet: BuildEtag -->
<a id='snippet-BuildEtag'></a>
```cs
internal static string BuildEtag(string timeStamp, string? suffix)
{
    if (suffix == null)
    {
        return $"\"{AssemblyWriteTime}-{timeStamp}\"";
    }

    return $"\"{AssemblyWriteTime}-{timeStamp}-{suffix}\"";
}
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L298-L310' title='Snippet source file'>snippet source</a> | <a href='#snippet-BuildEtag' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## NuGet

https://nuget.org/packages/Delta/


## Usage


### DbContext using RowVersion

Enable row versioning in Entity Framework

<!-- snippet: SampleDbContext.cs -->
<a id='snippet-SampleDbContext.cs'></a>
```cs
public class SampleDbContext(DbContextOptions options) :
    DbContext(options)
{
    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<Company> Companies { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var company = builder.Entity<Company>();
        company.HasKey(_ => _.Id);
        company
            .HasMany(_ => _.Employees)
            .WithOne(_ => _.Company)
            .IsRequired();
        company
            .Property(_ => _.RowVersion)
            .IsRowVersion()
            .HasConversion<byte[]>();

        var employee = builder.Entity<Employee>();
        employee.HasKey(_ => _.Id);
        employee
            .Property(_ => _.RowVersion)
            .IsRowVersion()
            .HasConversion<byte[]>();
    }
}
```
<sup><a href='/src/WebApplicationEF/DataContext/SampleDbContext.cs#L1-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-SampleDbContext.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Add to Builder

<!-- snippet: UseDelta -->
<a id='snippet-UseDelta'></a>
```cs
var builder = WebApplication.CreateBuilder();
builder.Services.AddScoped(_ => new SqlConnection(connectionString));
var app = builder.Build();
app.UseDelta(
    getConnection: httpContext => httpContext.RequestServices.GetRequiredService<SqlConnection>());
```
<sup><a href='/src/WebApplication/Program.cs#L10-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-UseDelta' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Add to a group

<!-- snippet: UseDeltaMapGroup -->
<a id='snippet-UseDeltaMapGroup'></a>
```cs
app.MapGroup("/group")
    .UseDelta(
        getConnection: httpContext => httpContext.RequestServices.GetRequiredService<SqlConnection>())
    .MapGet("/", () => "Hello Group!");
```
<sup><a href='/src/WebApplication/Program.cs#L59-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-UseDeltaMapGroup' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### ShouldExecute

Optional control what requests Delta is executed on.

<!-- snippet: ShouldExecute -->
<a id='snippet-ShouldExecute'></a>
```cs
var app = builder.Build();
app.UseDelta(
    getConnection: httpContext => httpContext.RequestServices.GetRequiredService<SqlConnection>(),
    shouldExecute: httpContext =>
    {
        var path = httpContext.Request.Path.ToString();
        return path.Contains("match");
    });
```
<sup><a href='/src/DeltaTests/Usage.cs#L21-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-ShouldExecute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## EF/SQL helpers


### GetLastTimeStamp


#### For a `DbContext`:

<!-- snippet: GetLastTimeStampDbContext -->
<a id='snippet-GetLastTimeStampDbContext'></a>
```cs
var timeStamp = await DeltaExtensions.GetLastTimeStamp(sqlConnection, null);
```
<sup><a href='/src/DeltaTests/Usage.cs#L63-L67' title='Snippet source file'>snippet source</a> | <a href='#snippet-GetLastTimeStampDbContext' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### For a `SqlConnection`:

<!-- snippet: GetLastTimeStampSqlConnection -->
<a id='snippet-GetLastTimeStampSqlConnection'></a>
```cs
var timeStamp = await sqlConnection.GetLastTimeStamp();
```
<sup><a href='/src/DeltaTests/Usage.cs#L79-L83' title='Snippet source file'>snippet source</a> | <a href='#snippet-GetLastTimeStampSqlConnection' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### GetDatabasesWithTracking

Get a list of all databases with change tracking enabled.

<!-- snippet: GetDatabasesWithTracking -->
<a id='snippet-GetDatabasesWithTracking'></a>
```cs
var trackedDatabases = await sqlConnection.GetTrackedDatabases();
foreach (var db in trackedDatabases)
{
    Trace.WriteLine(db);
}
```
<sup><a href='/src/DeltaTests/Usage.cs#L117-L125' title='Snippet source file'>snippet source</a> | <a href='#snippet-GetDatabasesWithTracking' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Uses the following SQL:

<!-- snippet: GetTrackedDatabasesSql -->
<a id='snippet-GetTrackedDatabasesSql'></a>
```cs
select d.name
from sys.databases as d inner join
  sys.change_tracking_databases as t on
  t.database_id = d.database_id
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L164-L169' title='Snippet source file'>snippet source</a> | <a href='#snippet-GetTrackedDatabasesSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### GetTrackedTables

Get a list of all tracked tables in database.

<!-- snippet: GetTrackedTables -->
<a id='snippet-GetTrackedTables'></a>
```cs
var trackedTables = await sqlConnection.GetTrackedTables();
foreach (var db in trackedTables)
{
    Trace.WriteLine(db);
}
```
<sup><a href='/src/DeltaTests/Usage.cs#L143-L151' title='Snippet source file'>snippet source</a> | <a href='#snippet-GetTrackedTables' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Uses the following SQL:

<!-- snippet: GetTrackedTablesSql -->
<a id='snippet-GetTrackedTablesSql'></a>
```cs
select t.Name
from sys.tables as t left join
  sys.change_tracking_tables as c on t.[object_id] = c.[object_id]
where c.[object_id] is not null
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L102-L107' title='Snippet source file'>snippet source</a> | <a href='#snippet-GetTrackedTablesSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### IsTrackingEnabled

Determine if change tracking is enabled for a database.

<!-- snippet: IsTrackingEnabled -->
<a id='snippet-IsTrackingEnabled'></a>
```cs
var isTrackingEnabled = await sqlConnection.IsTrackingEnabled();
```
<sup><a href='/src/DeltaTests/Usage.cs#L201-L205' title='Snippet source file'>snippet source</a> | <a href='#snippet-IsTrackingEnabled' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Uses the following SQL:

<!-- snippet: IsTrackingEnabledSql -->
<a id='snippet-IsTrackingEnabledSql'></a>
```cs
select count(d.name)
from sys.databases as d inner join
  sys.change_tracking_databases as t on
  t.database_id = d.database_id
where d.name = '{connection.Database}'
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L122-L128' title='Snippet source file'>snippet source</a> | <a href='#snippet-IsTrackingEnabledSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### EnableTracking

Enable change tracking for a database.

<!-- snippet: EnableTracking -->
<a id='snippet-EnableTracking'></a>
```cs
await sqlConnection.EnableTracking();
```
<sup><a href='/src/DeltaTests/Usage.cs#L195-L199' title='Snippet source file'>snippet source</a> | <a href='#snippet-EnableTracking' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Uses the following SQL:

<!-- snippet: EnableTrackingSql -->
<a id='snippet-EnableTrackingSql'></a>
```cs
alter database {connection.Database}
set change_tracking = on
(
  change_retention = {retentionDays} days,
  auto_cleanup = on
)
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L87-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-EnableTrackingSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### DisableTracking

Disable change tracking for a database and all tables within that database.

<!-- snippet: DisableTracking -->
<a id='snippet-DisableTracking'></a>
```cs
await sqlConnection.DisableTracking();
```
<sup><a href='/src/DeltaTests/Usage.cs#L180-L184' title='Snippet source file'>snippet source</a> | <a href='#snippet-DisableTracking' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Uses the following SQL:

<!-- snippet: DisableTrackingSql -->
<a id='snippet-DisableTrackingSql'></a>
```cs
alter table [{table}] disable change_tracking;
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L143-L145' title='Snippet source file'>snippet source</a> | <a href='#snippet-DisableTrackingSql' title='Start of snippet'>anchor</a></sup>
<a id='snippet-DisableTrackingSql-1'></a>
```cs
alter database [{connection.Database}] set change_tracking = off;
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L150-L152' title='Snippet source file'>snippet source</a> | <a href='#snippet-DisableTrackingSql-1' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### SetTrackedTables

Enables change tracking for all tables listed, and disables change tracking for all tables not listed.

<!-- snippet: SetTrackedTables -->
<a id='snippet-SetTrackedTables'></a>
```cs
await sqlConnection.SetTrackedTables(["Companies"]);
```
<sup><a href='/src/DeltaTests/Usage.cs#L137-L141' title='Snippet source file'>snippet source</a> | <a href='#snippet-SetTrackedTables' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Uses the following SQL:

<!-- snippet: EnableTrackingSql -->
<a id='snippet-EnableTrackingSql'></a>
```cs
alter database {connection.Database}
set change_tracking = on
(
  change_retention = {retentionDays} days,
  auto_cleanup = on
)
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L87-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-EnableTrackingSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: EnableTrackingTableSql -->
<a id='snippet-EnableTrackingTableSql'></a>
```cs
alter table [{table}] enable change_tracking
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L51-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-EnableTrackingTableSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: DisableTrackingTableSql -->
<a id='snippet-DisableTrackingTableSql'></a>
```cs
alter table [{table}] disable change_tracking;
```
<sup><a href='/src/Delta/DeltaExtensions_Shared.cs#L60-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-DisableTrackingTableSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## Icon

[Estuary](https://thenounproject.com/term/estuary/1847616/) designed by [Daan](https://thenounproject.com/Asphaleia/) from [The Noun Project](https://thenounproject.com).
