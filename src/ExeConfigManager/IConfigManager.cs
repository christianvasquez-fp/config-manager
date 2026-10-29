using System.Xml.Linq;

namespace ExeConfigManager;

/// <summary>
/// Provides strongly-typed and flexible operations to create, read, update, and delete
/// local .exe.config files, automatically saving changes on every mutation.
/// </summary>
public interface IConfigManager
{
    /// <summary>
    /// Gets the full path to the target .exe.config file on disk.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Checks whether the .exe.config file currently exists on disk.
    /// </summary>
    /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
    bool Exists();

    /// <summary>
    /// Creates a new .exe.config file with a root <c>&lt;configuration&gt;</c> element
    /// and saves it to disk. If the file already exists, it is overwritten when <paramref name="overwrite"/> is <c>true</c>.
    /// </summary>
    /// <param name="overwrite">Whether to overwrite an existing file.</param>
    void Create(bool overwrite = false);

    /// <summary>
    /// Loads and returns the underlying XML document of the .exe.config file.
    /// </summary>
    /// <returns>The loaded <see cref="XDocument"/>.</returns>
    XDocument ReadDocument();

    /// <summary>
    /// Removes the .exe.config file from disk if it exists.
    /// </summary>
    /// <returns><c>true</c> if the file existed and was removed; otherwise, <c>false</c>.</returns>
    bool Remove();

    /// <summary>
    /// Reads all key-value pairs from the <c>&lt;appSettings&gt;</c> section.
    /// </summary>
    /// <returns>A read-only dictionary of all application settings.</returns>
    IReadOnlyDictionary<string, string> ReadSettings();

    /// <summary>
    /// Reads a strongly-typed value from the <c>&lt;appSettings&gt;</c> section by key.
    /// </summary>
    /// <typeparam name="T">The target type to convert the setting value to.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The value to return if the key is not found.</param>
    /// <returns>The converted setting value, or <paramref name="defaultValue"/> if not found.</returns>
    T? ReadSetting<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Adds or updates a setting in the <c>&lt;appSettings&gt;</c> section and automatically saves the file to disk.
    /// </summary>
    /// <typeparam name="T">The type of the value being stored.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to store.</param>
    void SetSetting<T>(string key, T value);

    /// <summary>
    /// Removes a setting by key from the <c>&lt;appSettings&gt;</c> section and automatically saves the file to disk.
    /// </summary>
    /// <param name="key">The setting key to remove.</param>
    /// <returns><c>true</c> if the key was found and removed; otherwise, <c>false</c>.</returns>
    bool RemoveSetting(string key);

    /// <summary>
    /// Reads all name-connectionString pairs from the <c>&lt;connectionStrings&gt;</c> section.
    /// </summary>
    /// <returns>A read-only dictionary of all connection strings by name.</returns>
    IReadOnlyDictionary<string, string> ReadConnectionStrings();

    /// <summary>
    /// Reads a connection string value from the <c>&lt;connectionStrings&gt;</c> section by name.
    /// </summary>
    /// <param name="name">The connection string name.</param>
    /// <returns>The connection string value, or <c>null</c> if not found.</returns>
    string? ReadConnectionString(string name);

    /// <summary>
    /// Adds or updates a connection string in the <c>&lt;connectionStrings&gt;</c> section and automatically saves the file to disk.
    /// </summary>
    /// <param name="name">The connection string name.</param>
    /// <param name="connectionString">The connection string value.</param>
    /// <param name="providerName">Optional database provider invariant name.</param>
    void SetConnectionString(string name, string connectionString, string? providerName = null);

    /// <summary>
    /// Removes a connection string by name from the <c>&lt;connectionStrings&gt;</c> section and automatically saves the file to disk.
    /// </summary>
    /// <param name="name">The connection string name to remove.</param>
    /// <returns><c>true</c> if the connection string was found and removed; otherwise, <c>false</c>.</returns>
    bool RemoveConnectionString(string name);

    /// <summary>
    /// Reads all WCF client endpoint name-address pairs from <c>&lt;system.serviceModel&gt;&lt;client&gt;</c>.
    /// </summary>
    /// <returns>A read-only dictionary of endpoint names and their addresses.</returns>
    IReadOnlyDictionary<string, string> ReadEndpointAddresses();

    /// <summary>
    /// Reads a WCF client endpoint address by endpoint name from <c>&lt;system.serviceModel&gt;&lt;client&gt;</c>.
    /// </summary>
    /// <param name="endpointName">The endpoint <c>name</c> attribute value.</param>
    /// <returns>The endpoint address, or <c>null</c> if not found.</returns>
    string? ReadEndpointAddress(string endpointName);

    /// <summary>
    /// Updates the <c>address</c> attribute of an existing WCF client endpoint in <c>&lt;system.serviceModel&gt;&lt;client&gt;</c>
    /// and automatically saves the file to disk.
    /// </summary>
    /// <param name="endpointName">The endpoint <c>name</c> attribute value.</param>
    /// <param name="address">The new endpoint address.</param>
    void SetEndpointAddress(string endpointName, string address);

    /// <summary>
    /// Reads a strongly-typed attribute value from an XML element matched by XPath.
    /// </summary>
    /// <typeparam name="T">The target type to convert the attribute value to.</typeparam>
    /// <param name="xpath">The XPath expression selecting the element.</param>
    /// <param name="attributeName">The name of the attribute to read.</param>
    /// <param name="defaultValue">The value to return if the element or attribute is not found.</param>
    /// <returns>The converted attribute value, or <paramref name="defaultValue"/> if not found.</returns>
    T? ReadAttribute<T>(string xpath, string attributeName, T? defaultValue = default);

    /// <summary>
    /// Sets a strongly-typed attribute value on an XML element matched by XPath and automatically saves the file to disk.
    /// </summary>
    /// <typeparam name="T">The type of the attribute value being stored.</typeparam>
    /// <param name="xpath">The XPath expression selecting the element.</param>
    /// <param name="attributeName">The name of the attribute to set.</param>
    /// <param name="value">The value to store on the attribute.</param>
    void SetAttribute<T>(string xpath, string attributeName, T value);

    /// <summary>
    /// Removes an XML element matched by XPath and automatically saves the file to disk.
    /// </summary>
    /// <param name="xpath">The XPath expression selecting the element to remove.</param>
    /// <returns><c>true</c> if the element was found and removed; otherwise, <c>false</c>.</returns>
    bool RemoveElement(string xpath);
}
