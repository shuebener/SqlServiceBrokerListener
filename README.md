# SqlServiceBrokerListener

Cross-platform SQL Server Service Broker listener that streams table change notifications into your .NET code.

- Targets: .NET Framework 4.7.2 and .NET 8/9/10
- Client provider: Microsoft.Data.SqlClient
- Original concept based on ServiceBrokerListener (SqlDependencyEx)

Take a look how it works: http://sbl.azurewebsites.net

# Install

Use NuGet to add the library to your project:

- Package Manager Console
  Install-Package SqlServiceBrokerListener

- .NET CLI
  dotnet add package SqlServiceBrokerListener

# How To Use

1. Ensure that Service Broker is enabled for your database.
    
    ```sql
    ALTER DATABASE test SET ENABLE_BROKER;
    
    -- For SQL Express (example)
    ALTER AUTHORIZATION ON DATABASE::test TO sa;
    ```
    
2. Create a listener and handle notifications:

    ```csharp
    using SqlServiceBrokerListener;
    
    var listener = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable");
    
    // e.Data contains changed rows in XML (inserted/deleted)
    listener.TableChanged += (o, e) =>
    {
        Console.WriteLine($"Notification: {e.NotificationType}");
        Console.WriteLine(e.Data?.ToString());
    };
    
    listener.Start();
    // ... your code ...
    listener.Stop();
    ```

# How to use for multiple tables

Create multiple listeners with different identities:

```csharp
var listener1 = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable1", identity: 1);
var listener2 = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable2", identity: 2);
```
    
# How to use in multiple apps

Use unique identities per app instance to ensure proper cleanup:

Application 1:

```csharp
var listener = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable", identity: 1);
```
    
Application 2:

```csharp
var listener = new SqlDependencyEx(connectionString, "YourDatabase", "YourTable", identity: 2);
```

# How to track UPDATEs only

Configure the `listenerType` constructor parameter to fire an event only for specific change types:

```csharp
var listener = new SqlDependencyEx(
    connectionString,
    "YourDatabase",
    "YourTable",
    listenerType: SqlDependencyEx.NotificationTypes.Update);
```

# Notes

- Requires Service Broker to be enabled and the database user to have permissions to create procedures, queues, services, and triggers used by the listener.
- For high-throughput scenarios you can disable details: `receiveDetails: false` to get lightweight notifications.

# License

MIT
