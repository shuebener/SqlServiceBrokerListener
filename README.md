# SqlServiceBrokerListener

A .NET library for listening to SQL Server Service Broker notifications and reporting table inserts, updates, and deletes to your application.

- Targets .NET 10 and .NET 11.
- Uses `Microsoft.Data.SqlClient`.
- MIT licensed.

## Install

Add the NuGet package to your project:

```shell
dotnet add package SqlServiceBrokerListener
```

Or use the Package Manager Console:

```powershell
Install-Package SqlServiceBrokerListener
```

## Requirements

- SQL Server Service Broker must be enabled for the target database. For example:

  ```sql
  ALTER DATABASE [YourDatabase] SET ENABLE_BROKER;
  ```

- The database user in your connection string needs permissions to install and use the listener's Service Broker objects. These include `CREATE PROCEDURE`, `CREATE SERVICE`, `CREATE QUEUE`, `SUBSCRIBE QUERY NOTIFICATIONS`, `CONTROL` on the listener schema, and `REFERENCES` on the `DEFAULT` contract. Grant only the permissions appropriate for your environment.
- The listener's default schema is `dbo`.

## Listen for table changes

```csharp
using SqlServiceBrokerListener;

using var listener = new SqlDependencyEx(
    connectionString,
    "YourDatabase",
    "YourTable");

listener.TableChanged += (_, e) =>
{
    Console.WriteLine($"Change: {e.NotificationType}");
    Console.WriteLine(e.Data); // XML with inserted and/or deleted rows, when details are enabled
};

listener.Start();
// Keep the application running while notifications are needed.
// Dispose the listener (or call Stop()) when finished.
```

The constructor also accepts `schemaName` (default `"dbo"`), `listenerType`, `receiveDetails`, and `identity`. By default, the listener reports inserts, updates, and deletes and includes changed-row details. `e.NotificationType` is one of `Insert`, `Update`, `Delete`, or `None`.

To listen for updates only:

```csharp
var listener = new SqlDependencyEx(
    connectionString,
    "YourDatabase",
    "YourTable",
    listenerType: SqlDependencyEx.NotificationTypes.Update);
```

Set `receiveDetails: false` when you only need notification types and do not need the changed-row XML. In that mode, `e.Data` is null and `e.NotificationType` is `None`.

## Multiple listeners and application instances

Use a distinct `identity` for each listener that may be active at the same time. Choose identities that are unique across application instances that share the same database:

```csharp
var first = new SqlDependencyEx(connectionString, "YourDatabase", "Orders", identity: 1);
var second = new SqlDependencyEx(connectionString, "YourDatabase", "Customers", identity: 2);
```

## Command-line listener

The repository also includes a console listener. Run it with a connection string, database, and table:

```shell
dotnet run --project SqlServiceBrokerListener/SqlServiceBrokerListener.Tablelistener.Core -- \
  --connectionstring "Data Source=server;Initial Catalog=YourDatabase;Integrated Security=True;TrustServerCertificate=True" \
  --database YourDatabase \
  --table YourTable
```

Use `--help` to display the command-line options. The program waits for a key press; press `x` to stop listening.

## License

MIT
