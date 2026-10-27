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
    const string AddElementName = "add";
    const string KeyAttributeName = "key";
    const string ValueAttributeName = "value";

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
    public IReadOnlyDictionary<string, string> ReadSettings()
    {
        var document = ReadDocument();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var appSettings = document.Root!.Element(AppSettingsElementName);
        if (appSettings == null)
        {
            return result;
        }

        foreach (var addElement in appSettings.Elements(AddElementName))
        {
            var key = addElement.Attribute(KeyAttributeName)?.Value;
            if (!string.IsNullOrEmpty(key))
            {
                result[key!] = addElement.Attribute(ValueAttributeName)?.Value ?? string.Empty;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public T? ReadSetting<T>(string key, T? defaultValue = default)
    {
        ValidateNotNullOrWhiteSpace(key, nameof(key));

        return ReadSettings().TryGetValue(key, out var rawValue)
            ? ConvertValue<T>(rawValue)
            : defaultValue;
    }

    /// <inheritdoc />
    public void SetSetting<T>(string key, T value)
    {
        ValidateNotNullOrWhiteSpace(key, nameof(key));

        var document = ReadDocument();
        var root = document.Root!;
        var appSettings = root.Element(AppSettingsElementName);
        if (appSettings == null)
        {
            appSettings = new XElement(AppSettingsElementName);
            root.Add(appSettings);
        }

        var formattedValue = FormatValue(value);
        var existingElement = FindAppSettingElements(appSettings, key).LastOrDefault();
        if (existingElement != null)
        {
            existingElement.SetAttributeValue(ValueAttributeName, formattedValue);
        }
        else
        {
            appSettings.Add(new XElement(
                AddElementName,
                new XAttribute(KeyAttributeName, key),
                new XAttribute(ValueAttributeName, formattedValue)));
        }

        document.Save(FilePath);
    }

    /// <inheritdoc />
    public bool RemoveSetting(string key)
    {
        ValidateNotNullOrWhiteSpace(key, nameof(key));

        var document = ReadDocument();
        var appSettings = document.Root!.Element(AppSettingsElementName);
        if (appSettings == null)
        {
            return false;
        }

        var matchingElements = FindAppSettingElements(appSettings, key).ToList();
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

    static IEnumerable<XElement> FindAppSettingElements(XElement appSettings, string key) =>
        appSettings
            .Elements(AddElementName)
            .Where(element => string.Equals(element.Attribute(KeyAttributeName)?.Value, key, StringComparison.Ordinal));

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

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
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
