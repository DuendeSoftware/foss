using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Defines an optionally keyed dependency. 
/// If a keyed dependency is provided, it will be used, otherwise the default dependency will be used.
/// This can be used for example to register a different <see cref="IDistributedCache"/>
/// for <see cref="DistributedClientCredentialsTokenCache"/>.
/// </summary>
public static class OptionallyKeyedDependency
{
    /// <summary>
    /// By default, all duende software uses the same 'key' for keyed services. This makes it
    /// possible to override all keyed dependencies.
    ///
    /// If you wish to override a single usage of OptionallyKeyedDependency, create a derived class
    /// from OptionallyKeyedDependency and use the FromKeyedServices attribute to specify the key.
    /// </summary>
    public const string Duende = "duende";
}

/// <inheritdoc />
public class OptionallyKeyedDependency<T> where T : class
{

    private T _dependency;

    public OptionallyKeyedDependency(T defaultDependency, [FromKeyedServices(OptionallyKeyedDependency.Duende)] T? keyedDependency = null)
    {
        _dependency = keyedDependency ?? defaultDependency;
    }

    /// <summary>
    /// The actually resolved dependency. 
    /// </summary>
    public T Dependency => _dependency;
}