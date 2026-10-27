# ConfigManager

A lightweight, strongly-typed .NET Standard 2.0 library for creating, reading, updating, and removing local `.exe.config` files, automatically saving changes on every mutation.

## Installation

```bash
dotnet add package ExeConfigManager
```

## Quick Start

```csharp
using ConfigManager;

var manager = new ConfigManager.ConfigManager(@"C:\MyApp\MyApp.exe.config");

if (!manager.Exists())
{
    manager.Create();
}

// <appSettings>
manager.SetSetting("ServiceEnabled", true);
manager.SetSetting("RetryCount", 3);

var serviceEnabled = manager.ReadSetting<bool>("ServiceEnabled");
var retryCount = manager.ReadSetting("RetryCount", defaultValue: 5);
var settings = manager.ReadSettings();
manager.RemoveSetting("RetryCount");

// <connectionStrings>
manager.SetConnectionString("MainDatabase", "Server=localhost;Database=Application;", "System.Data.SqlClient");
var connectionString = manager.ReadConnectionString("MainDatabase");
var connectionStrings = manager.ReadConnectionStrings();
manager.RemoveConnectionString("MainDatabase");

// WCF <system.serviceModel><client><endpoint>
var endpointAddress = manager.ReadEndpointAddress("NetTcpBinding_IService");
var endpointAddresses = manager.ReadEndpointAddresses();
manager.SetEndpointAddress("NetTcpBinding_IService", "net.tcp://192.168.1.20:5000/Service");

// Custom XML elements and attributes via XPath
var jitDebugging = manager.ReadAttribute<bool>("/configuration/system.windows.forms", "jitDebugging");
manager.SetAttribute("/configuration/system.windows.forms", "jitDebugging", false);
manager.RemoveElement("/configuration/system.windows.forms");

// Remove the .exe.config file from disk
manager.Remove();
```

## License

[MIT](LICENSE)
