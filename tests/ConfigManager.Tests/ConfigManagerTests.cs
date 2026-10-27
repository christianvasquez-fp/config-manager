using System.Xml;
using NExpect;
using static NExpect.Expectations;

namespace ConfigManager.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ConfigManagerTests
{
    string directory = null!;
    string configurationPath = null!;
    ConfigManager manager = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "ConfigManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        configurationPath = Path.Combine(directory, "TestApp.exe.config");
        manager = new ConfigManager(configurationPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Constructor_WithValidPath_SetsFullFilePath()
    {
        Expect(manager.FilePath).To.Equal(Path.GetFullPath(configurationPath));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithNullOrWhiteSpacePath_ThrowsArgumentException(string? invalidPath)
    {
        Expect(() => _ = new ConfigManager(invalidPath!))
            .To.Throw<ArgumentException>();
    }

    [Test]
    public void Create_And_Exists_CreatesValidConfigurationXmlFile()
    {
        Expect(manager.Exists()).To.Be.False();

        manager.Create();

        Expect(manager.Exists()).To.Be.True();
        Expect(manager.ReadDocument().Root?.Name.LocalName).To.Equal("configuration");
        Expect(manager.ReadSettings()).To.Be.Empty();
    }

    [Test]
    public void Create_WhenParentDirectoryDoesNotExist_CreatesDirectoryAndFile()
    {
        var nestedPath = Path.Combine(directory, "nested", "subdirectory", "App.exe.config");
        var nestedManager = new ConfigManager(nestedPath);

        nestedManager.Create();

        Expect(File.Exists(nestedPath)).To.Be.True();
    }

    [Test]
    public void Create_WhenFileAlreadyExistsAndOverwriteIsFalse_ThrowsIOException()
    {
        manager.Create();

        Expect(() => manager.Create(overwrite: false))
            .To.Throw<IOException>();
    }

    [Test]
    public void Create_WhenFileAlreadyExistsAndOverwriteIsTrue_ResetsFileToCleanConfiguration()
    {
        manager.Create();
        manager.SetSetting("Key1", "Value1");

        manager.Create(overwrite: true);

        Expect(manager.ReadSettings()).To.Be.Empty();
    }

    [Test]
    public void ReadDocument_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        Expect(() => _ = manager.ReadDocument())
            .To.Throw<FileNotFoundException>();
    }

    [Test]
    public void ReadDocument_WhenFileContainsMalformedXml_ThrowsXmlException()
    {
        File.WriteAllText(configurationPath, "<configuration><unclosed>");

        Expect(() => _ = manager.ReadDocument())
            .To.Throw<XmlException>();
    }

    [Test]
    public void ReadDocument_WhenRootElementIsNotConfiguration_ThrowsInvalidOperationException()
    {
        File.WriteAllText(configurationPath, "<wrongRoot></wrongRoot>");

        Expect(() => _ = manager.ReadDocument())
            .To.Throw<InvalidOperationException>();
    }

    [Test]
    public void Remove_WhenFileExists_RemovesFileAndReturnsTrue()
    {
        manager.Create();

        Expect(manager.Remove()).To.Be.True();
        Expect(manager.Exists()).To.Be.False();
    }

    [Test]
    public void Remove_WhenFileDoesNotExist_ReturnsFalse()
    {
        Expect(manager.Remove()).To.Be.False();
    }

    [Test]
    public void SetSetting_And_ReadSetting_WithVariousTypes_PersistsToDiskAndConvertsCorrectly()
    {
        manager.Create();
        var identifier = Guid.NewGuid();

        manager.SetSetting("AppName", "FlashPARCS");
        manager.SetSetting("BLEServiceEnabled", true);
        manager.SetSetting("Location Delay", 33);
        manager.SetSetting("Threshold", 12.75);
        manager.SetSetting("Kiosk ID", identifier);

        var reader = new ConfigManager(configurationPath);
        Expect(reader.ReadSetting<string>("AppName")).To.Equal("FlashPARCS");
        Expect(reader.ReadSetting<bool>("BLEServiceEnabled")).To.Be.True();
        Expect(reader.ReadSetting<int>("Location Delay")).To.Equal(33);
        Expect(reader.ReadSetting<double>("Threshold")).To.Equal(12.75);
        Expect(reader.ReadSetting<Guid>("Kiosk ID")).To.Equal(identifier);
    }

    [Test]
    public void SetSetting_WhenKeyAlreadyExists_UpdatesExistingValueAndSaves()
    {
        manager.Create();
        manager.SetSetting("Location Delay", 33);

        manager.SetSetting("Location Delay", 99);

        var reader = new ConfigManager(configurationPath);
        Expect(reader.ReadSetting<int>("Location Delay")).To.Equal(99);
        Expect(reader.ReadSettings().Count).To.Equal(1);
    }

    [Test]
    public void SetSetting_WithNullValue_StoresEmptyString()
    {
        manager.Create();

        manager.SetSetting<string?>("BLEGateID", null);

        var reader = new ConfigManager(configurationPath);
        Expect(reader.ReadSetting<string>("BLEGateID")).To.Equal(string.Empty);
        Expect(reader.ReadSetting<int?>("BLEGateID")).To.Be.Null();
    }

    [Test]
    public void ReadSetting_WhenKeyDoesNotExist_ReturnsDefaultValue()
    {
        manager.Create();

        Expect(manager.ReadSetting<string>("Missing")).To.Be.Null();
        Expect(manager.ReadSetting("Missing", "Fallback")).To.Equal("Fallback");
        Expect(manager.ReadSetting("MissingInteger", 42)).To.Equal(42);
        Expect(manager.ReadSetting<int?>("MissingNullableInteger")).To.Be.Null();
    }

    [Test]
    public void ReadSetting_WhenValueCannotBeConvertedToTargetType_ThrowsFormatException()
    {
        manager.Create();
        manager.SetSetting("NotAnInteger", "abc");

        Expect(() => _ = manager.ReadSetting<int>("NotAnInteger"))
            .To.Throw<FormatException>();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void AppSettingMethods_WithNullOrWhiteSpaceKey_ThrowArgumentException(string? invalidKey)
    {
        manager.Create();

        Expect(() => _ = manager.ReadSetting<string>(invalidKey!)).To.Throw<ArgumentException>();
        Expect(() => manager.SetSetting(invalidKey!, "value")).To.Throw<ArgumentException>();
        Expect(() => _ = manager.RemoveSetting(invalidKey!)).To.Throw<ArgumentException>();
    }

    [Test]
    public void RemoveSetting_WhenKeyExists_RemovesFromDiskAndReturnsTrue()
    {
        manager.Create();
        manager.SetSetting("TemporaryKey", "123");

        Expect(manager.RemoveSetting("TemporaryKey")).To.Be.True();
        Expect(new ConfigManager(configurationPath).ReadSettings().ContainsKey("TemporaryKey")).To.Be.False();
    }

    [Test]
    public void RemoveSetting_WhenKeyDoesNotExist_ReturnsFalse()
    {
        manager.Create();

        Expect(manager.RemoveSetting("NonExistentKey")).To.Be.False();
    }

    [Test]
    public void ReadSettings_WhenDuplicateKeysExist_LastKeyWins()
    {
        File.WriteAllText(configurationPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="DuplicateKey" value="First" />
                <add key="DuplicateKey" value="Second" />
              </appSettings>
            </configuration>
            """);

        Expect(manager.ReadSettings()["DuplicateKey"]).To.Equal("Second");
    }

    [Test]
    public void ReadAttribute_And_SetAttribute_UpdatesXmlElementAndPersistsToDisk()
    {
        File.WriteAllText(configurationPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <system.windows.forms jitDebugging="true" />
              <system.serviceModel>
                <client>
                  <endpoint name="NetTcpBinding_IBOFService" address="net.tcp://localhost:5000/BOFService" />
                </client>
              </system.serviceModel>
            </configuration>
            """);

        Expect(manager.ReadAttribute<bool>("/configuration/system.windows.forms", "jitDebugging")).To.Be.True();

        manager.SetAttribute("/configuration/system.windows.forms", "jitDebugging", false);
        manager.SetAttribute(
            "/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IBOFService']",
            "address",
            "net.tcp://10.0.0.99:5000/BOFService");

        var reader = new ConfigManager(configurationPath);
        Expect(reader.ReadAttribute<bool>("/configuration/system.windows.forms", "jitDebugging")).To.Be.False();
        Expect(reader.ReadAttribute<string>("/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IBOFService']", "address"))
            .To.Equal("net.tcp://10.0.0.99:5000/BOFService");
    }

    [Test]
    public void ReadAttribute_WhenElementOrAttributeDoesNotExist_ReturnsDefaultValue()
    {
        manager.Create();

        Expect(manager.ReadAttribute("/configuration/missing", "attribute", "defaultValue")).To.Equal("defaultValue");
        Expect(manager.ReadAttribute("/configuration", "missingAttribute", 100)).To.Equal(100);
    }

    [Test]
    public void SetAttribute_WhenElementDoesNotExist_ThrowsInvalidOperationException()
    {
        manager.Create();

        Expect(() => manager.SetAttribute("/configuration/nonExistentNode", "attribute", "value"))
            .To.Throw<InvalidOperationException>();
    }

    [Test]
    public void RemoveElement_WhenElementExists_RemovesAndPersistsToDisk()
    {
        File.WriteAllText(configurationPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <system.windows.forms jitDebugging="true" />
            </configuration>
            """);

        Expect(manager.RemoveElement("/configuration/system.windows.forms")).To.Be.True();
        Expect(new ConfigManager(configurationPath).ReadDocument().Root?.Element("system.windows.forms")).To.Be.Null();
    }

    [Test]
    public void RemoveElement_WhenElementDoesNotExist_ReturnsFalse()
    {
        manager.Create();

        Expect(manager.RemoveElement("/configuration/nonExistent")).To.Be.False();
    }

    [Test]
    public void RemoveElement_WhenTargetIsRootConfiguration_ThrowsInvalidOperationException()
    {
        manager.Create();

        Expect(() => manager.RemoveElement("/configuration"))
            .To.Throw<InvalidOperationException>();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void XPathMethods_WithNullOrWhiteSpaceArguments_ThrowArgumentException(string? invalidArgument)
    {
        manager.Create();

        Expect(() => _ = manager.ReadAttribute<string>(invalidArgument!, "attribute")).To.Throw<ArgumentException>();
        Expect(() => _ = manager.ReadAttribute<string>("/configuration", invalidArgument!)).To.Throw<ArgumentException>();
        Expect(() => manager.SetAttribute(invalidArgument!, "attribute", "value")).To.Throw<ArgumentException>();
        Expect(() => manager.SetAttribute("/configuration", invalidArgument!, "value")).To.Throw<ArgumentException>();
        Expect(() => _ = manager.RemoveElement(invalidArgument!)).To.Throw<ArgumentException>();
    }

    [Test]
    public void SetConnectionString_And_ReadConnectionString_PersistsToDiskAndUpdatesExisting()
    {
        manager.Create();

        manager.SetConnectionString("MainDatabase", "Server=localhost;Database=Parking;", "System.Data.SqlClient");
        manager.SetConnectionString("LocalCache", "Data Source=cache.db");

        var reader = new ConfigManager(configurationPath);
        Expect(reader.ReadConnectionStrings().Count).To.Equal(2);
        Expect(reader.ReadConnectionString("MainDatabase")).To.Equal("Server=localhost;Database=Parking;");
        Expect(reader.ReadAttribute<string>("/configuration/connectionStrings/add[@name='MainDatabase']", "providerName"))
            .To.Equal("System.Data.SqlClient");

        manager.SetConnectionString("MainDatabase", "Server=10.0.0.1;Database=Parking;");

        Expect(new ConfigManager(configurationPath).ReadConnectionString("MainDatabase"))
            .To.Equal("Server=10.0.0.1;Database=Parking;");
    }

    [Test]
    public void ReadConnectionString_And_RemoveConnectionString_WhenMissing_ReturnsNullOrFalse()
    {
        manager.Create();

        Expect(manager.ReadConnectionStrings()).To.Be.Empty();
        Expect(manager.ReadConnectionString("Missing")).To.Be.Null();
        Expect(manager.RemoveConnectionString("Missing")).To.Be.False();
    }

    [Test]
    public void RemoveConnectionString_WhenExists_RemovesFromDiskAndReturnsTrue()
    {
        manager.Create();
        manager.SetConnectionString("MainDatabase", "Server=localhost;");

        Expect(manager.RemoveConnectionString("MainDatabase")).To.Be.True();
        Expect(new ConfigManager(configurationPath).ReadConnectionString("MainDatabase")).To.Be.Null();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ConnectionStringMethods_WithNullOrWhiteSpaceArguments_ThrowArgumentException(string? invalidArgument)
    {
        manager.Create();

        Expect(() => _ = manager.ReadConnectionString(invalidArgument!)).To.Throw<ArgumentException>();
        Expect(() => manager.SetConnectionString(invalidArgument!, "Server=localhost;")).To.Throw<ArgumentException>();
        Expect(() => manager.SetConnectionString("MainDatabase", invalidArgument!)).To.Throw<ArgumentException>();
        Expect(() => _ = manager.RemoveConnectionString(invalidArgument!)).To.Throw<ArgumentException>();
    }

    [Test]
    public void ReadEndpointAddress_And_SetEndpointAddress_ReadsAndUpdatesWcfClientEndpoints()
    {
        File.WriteAllText(configurationPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="net.tcp://localhost:5000/AuthenticationService" binding="netTcpBinding" name="NetTcpBinding_IAuthenticationService" />
                  <endpoint address="net.tcp://localhost:5000/BOFService" binding="netTcpBinding" name="NetTcpBinding_IBOFService" />
                </client>
              </system.serviceModel>
            </configuration>
            """);

        Expect(manager.ReadEndpointAddresses().Count).To.Equal(2);
        Expect(manager.ReadEndpointAddress("NetTcpBinding_IBOFService")).To.Equal("net.tcp://localhost:5000/BOFService");

        manager.SetEndpointAddress("NetTcpBinding_IBOFService", "net.tcp://192.168.1.20:5000/BOFService");

        Expect(new ConfigManager(configurationPath).ReadEndpointAddress("NetTcpBinding_IBOFService"))
            .To.Equal("net.tcp://192.168.1.20:5000/BOFService");
    }

    [Test]
    public void EndpointAddressMethods_WhenMissing_ReturnEmptyOrNullOrThrowOnSet()
    {
        manager.Create();

        Expect(manager.ReadEndpointAddresses()).To.Be.Empty();
        Expect(manager.ReadEndpointAddress("MissingEndpoint")).To.Be.Null();
        Expect(() => manager.SetEndpointAddress("MissingEndpoint", "net.tcp://localhost:5000/Missing"))
            .To.Throw<InvalidOperationException>();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void EndpointAddressMethods_WithNullOrWhiteSpaceArguments_ThrowArgumentException(string? invalidArgument)
    {
        manager.Create();

        Expect(() => _ = manager.ReadEndpointAddress(invalidArgument!)).To.Throw<ArgumentException>();
        Expect(() => manager.SetEndpointAddress(invalidArgument!, "net.tcp://localhost:5000/Service")).To.Throw<ArgumentException>();
        Expect(() => manager.SetEndpointAddress("ServiceEndpoint", invalidArgument!)).To.Throw<ArgumentException>();
    }
}
