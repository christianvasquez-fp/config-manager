using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ConfigManager;

/// <summary>
/// Manages local .exe.config files, automatically saving changes on every mutation.
/// </summary>
public class ConfigManager : IConfigManager
{
    const string RootElementName = "configuration";
    const string AppSettingsElementName = "appSettings";
    const string ConnectionStringsElementName = "connectionStrings";
    const string SystemServiceModelElementName = "system.serviceModel";
    const string ClientElementName = "client";
    const string EndpointElementName = "endpoint";
    const string AddElementName = "add";
    const string KeyAttributeName = "key";
    const string ValueAttributeName = "value";
    const string NameAttributeName = "name";
    const string ConnectionStringAttributeName = "connectionString";
    const string ProviderNameAttributeName = "providerName";
    const string AddressAttributeName = "address";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigManager"/> class for the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the .exe.config file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null, empty, or whitespace.</exception>
    public ConfigManager(string filePath)
    {
        ValidateNotNullOrWhiteSpace(filePath, nameof(filePath));
        FilePath = Path.GetFullPath(filePath);
    }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public bool Exists() => File.Exists(FilePath);

    /// <inheritdoc />
    public void Create(bool overwrite = false)
    {
        if (Exists() && !overwrite)
        {
            throw new IOException($"The configuration file '{FilePath}' already exists.");
        }

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(RootElementName));

        document.Save(FilePath);
    }

    /// <inheritdoc />
    public XDocument ReadDocument()
    {
        if (!Exists())
        {
            throw new FileNotFoundException($"The configuration file '{FilePath}' was not found.", FilePath);
        }

        var document = XDocument.Load(FilePath);
        if (!string.Equals(document.Root?.Name.LocalName, RootElementName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The configuration file '{FilePath}' does not have a valid '<{RootElementName}>' root element.");
        }

        return document;
    }

    /// <inheritdoc />
    public bool Remove()
    {
        if (!Exists())
        {
            return false;
        }

        File.Delete(FilePath);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ReadSettings() =>
        ReadPairs(ReadDocument().Root!.Element(AppSettingsElementName), AddElementName, KeyAttributeName, ValueAttributeName);

    /// <inheritdoc />
    public T? ReadSetting<T>(string key, T? defaultValue = default)
    {
        ValidateNotNullOrWhiteSpace(key, nameof(key));

        return TryReadValueByAttribute(
            ReadDocument().Root!.Element(AppSettingsElementName),
            AddElementName,
            KeyAttributeName,
            key,
            ValueAttributeName,
            out var rawValue)
            ? ConvertValue<T>(rawValue)
            : defaultValue;
    }

    /// <inheritdoc />
    public void SetSetting<T>(string key, T value)
    {
        ValidateNotNullOrWhiteSpace(key, nameof(key));

        var document = ReadDocument();
        var appSettings = GetOrCreateChildElement(document.Root!, AppSettingsElementName);
        var element = GetOrCreateElementByAttribute(appSettings, AddElementName, KeyAttributeName, key);

        element.SetAttributeValue(ValueAttributeName, FormatValue(value));
        document.Save(FilePath);
    }

    /// <inheritdoc />
    public bool RemoveSetting(string key)
    {
        ValidateNotNullOrWhiteSpace(key, nameof(key));
        return RemoveMatchingChildElements(AppSettingsElementName, AddElementName, KeyAttributeName, key);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ReadConnectionStrings() =>
        ReadPairs(ReadDocument().Root!.Element(ConnectionStringsElementName), AddElementName, NameAttributeName, ConnectionStringAttributeName);

    /// <inheritdoc />
    public string? ReadConnectionString(string name)
    {
        ValidateNotNullOrWhiteSpace(name, nameof(name));

        return TryReadValueByAttribute(
            ReadDocument().Root!.Element(ConnectionStringsElementName),
            AddElementName,
            NameAttributeName,
            name,
            ConnectionStringAttributeName,
            out var connectionString)
            ? connectionString
            : null;
    }

    /// <inheritdoc />
    public void SetConnectionString(string name, string connectionString, string? providerName = null)
    {
        ValidateNotNullOrWhiteSpace(name, nameof(name));
        ValidateNotNullOrWhiteSpace(connectionString, nameof(connectionString));

        var document = ReadDocument();
        var connectionStrings = GetOrCreateChildElement(document.Root!, ConnectionStringsElementName);
        var element = GetOrCreateElementByAttribute(connectionStrings, AddElementName, NameAttributeName, name);

        element.SetAttributeValue(ConnectionStringAttributeName, connectionString);
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            element.SetAttributeValue(ProviderNameAttributeName, providerName);
        }

        document.Save(FilePath);
    }

    /// <inheritdoc />
    public bool RemoveConnectionString(string name)
    {
        ValidateNotNullOrWhiteSpace(name, nameof(name));
        return RemoveMatchingChildElements(ConnectionStringsElementName, AddElementName, NameAttributeName, name);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ReadEndpointAddresses() =>
        ReadPairs(GetClientSection(ReadDocument()), EndpointElementName, NameAttributeName, AddressAttributeName);

    /// <inheritdoc />
    public string? ReadEndpointAddress(string endpointName)
    {
        ValidateNotNullOrWhiteSpace(endpointName, nameof(endpointName));

        return TryReadValueByAttribute(
            GetClientSection(ReadDocument()),
            EndpointElementName,
            NameAttributeName,
            endpointName,
            AddressAttributeName,
            out var address)
            ? address
            : null;
    }

    /// <inheritdoc />
    public void SetEndpointAddress(string endpointName, string address)
    {
        ValidateNotNullOrWhiteSpace(endpointName, nameof(endpointName));
        ValidateNotNullOrWhiteSpace(address, nameof(address));

        var document = ReadDocument();
        var clientSection = GetClientSection(document);
        var endpointElement = clientSection != null
            ? FindElementsByAttribute(clientSection, EndpointElementName, NameAttributeName, endpointName).LastOrDefault()
            : null;

        if (endpointElement == null)
        {
            throw new InvalidOperationException($"No WCF client endpoint found with name '{endpointName}'.");
        }

        endpointElement.SetAttributeValue(AddressAttributeName, address);
        document.Save(FilePath);
    }

    /// <inheritdoc />
    public T? ReadAttribute<T>(string xpath, string attributeName, T? defaultValue = default)
    {
        ValidateNotNullOrWhiteSpace(xpath, nameof(xpath));
        ValidateNotNullOrWhiteSpace(attributeName, nameof(attributeName));

        var attribute = ReadDocument().XPathSelectElement(xpath)?.Attribute(attributeName);
        return attribute != null
            ? ConvertValue<T>(attribute.Value)
            : defaultValue;
    }

    /// <inheritdoc />
    public void SetAttribute<T>(string xpath, string attributeName, T value)
    {
        ValidateNotNullOrWhiteSpace(xpath, nameof(xpath));
        ValidateNotNullOrWhiteSpace(attributeName, nameof(attributeName));

        var document = ReadDocument();
        var element = document.XPathSelectElement(xpath)
            ?? throw new InvalidOperationException($"No XML element matched the XPath expression '{xpath}'.");

        element.SetAttributeValue(attributeName, FormatValue(value));
        document.Save(FilePath);
    }

    /// <inheritdoc />
    public bool RemoveElement(string xpath)
    {
        ValidateNotNullOrWhiteSpace(xpath, nameof(xpath));

        var document = ReadDocument();
        var element = document.XPathSelectElement(xpath);
        if (element == null)
        {
            return false;
        }

        if (element == document.Root)
        {
            throw new InvalidOperationException("Cannot remove the root '<configuration>' element.");
        }

        element.Remove();
        document.Save(FilePath);
        return true;
    }

    bool RemoveMatchingChildElements(string sectionName, string elementName, string keyAttributeName, string keyValue)
    {
        var document = ReadDocument();
        var section = document.Root!.Element(sectionName);
        if (section == null)
        {
            return false;
        }

        var matchingElements = FindElementsByAttribute(section, elementName, keyAttributeName, keyValue).ToList();
        if (matchingElements.Count == 0)
        {
            return false;
        }

        foreach (var element in matchingElements)
        {
            element.Remove();
        }

        document.Save(FilePath);
        return true;
    }

    static XElement? GetClientSection(XDocument document) =>
        document.Root?
            .Element(SystemServiceModelElementName)?
            .Element(ClientElementName);

    static XElement GetOrCreateChildElement(XElement parent, string elementName)
    {
        var child = parent.Element(elementName);
        if (child == null)
        {
            child = new XElement(elementName);
            parent.Add(child);
        }

        return child;
    }

    static XElement GetOrCreateElementByAttribute(
        XElement parent,
        string elementName,
        string attributeName,
        string attributeValue)
    {
        var element = FindElementsByAttribute(parent, elementName, attributeName, attributeValue).LastOrDefault();
        if (element == null)
        {
            element = new XElement(elementName, new XAttribute(attributeName, attributeValue));
            parent.Add(element);
        }

        return element;
    }

    static IReadOnlyDictionary<string, string> ReadPairs(
        XElement? section,
        string childElementName,
        string keyAttributeName,
        string valueAttributeName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (section == null)
        {
            return result;
        }

        foreach (var element in section.Elements(childElementName))
        {
            var key = element.Attribute(keyAttributeName)?.Value;
            if (!string.IsNullOrEmpty(key))
            {
                result[key!] = element.Attribute(valueAttributeName)?.Value ?? string.Empty;
            }
        }

        return result;
    }

    static bool TryReadValueByAttribute(
        XElement? section,
        string childElementName,
        string keyAttributeName,
        string keyValue,
        string valueAttributeName,
        out string value)
    {
        if (section != null)
        {
            var element = FindElementsByAttribute(section, childElementName, keyAttributeName, keyValue).LastOrDefault();
            if (element != null)
            {
                value = element.Attribute(valueAttributeName)?.Value ?? string.Empty;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    static IEnumerable<XElement> FindElementsByAttribute(
        XElement parent,
        string elementName,
        string attributeName,
        string attributeValue) =>
        parent
            .Elements(elementName)
            .Where(element => string.Equals(element.Attribute(attributeName)?.Value, attributeValue, StringComparison.Ordinal));

    static void ValidateNotNullOrWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be null or whitespace.", parameterName);
        }
    }

    static string FormatValue<T>(T value) =>
        value switch
        {
            null => string.Empty,
            bool booleanValue => booleanValue ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

    static T? ConvertValue<T>(string rawValue)
    {
        var targetType = typeof(T);
        if (targetType == typeof(string))
        {
            return (T)(object)rawValue;
        }

        var nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlyingType != null && string.IsNullOrWhiteSpace(rawValue))
        {
            return default;
        }

        var underlyingType = nullableUnderlyingType ?? targetType;
        var converter = TypeDescriptor.GetConverter(underlyingType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                return (T?)converter.ConvertFromInvariantString(rawValue);
            }
            catch (Exception exception) when (exception is not FormatException)
            {
                throw new FormatException($"Cannot convert '{rawValue}' to type '{underlyingType.Name}'.", exception);
            }
        }

        return (T)Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
    }
}
