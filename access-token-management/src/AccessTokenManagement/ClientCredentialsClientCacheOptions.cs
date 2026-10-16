// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Per-client cache settings for a client credentials client. These are configured as named options,
/// using the client name as the options name, for example with
/// <see cref="ClientCredentialsTokenManagementBuilder.ConfigureClientCache"/> or
/// <c>services.Configure&lt;ClientCredentialsClientCacheOptions&gt;(name, ...)</c>.
///
/// Any property left null uses the corresponding <see cref="ClientCredentialsTokenManagementOptions"/> value.
/// These settings apply whether or not the client was registered with
/// <see cref="ClientCredentialsTokenManagementBuilder.AddClient"/>.
/// </summary>
public sealed class ClientCredentialsClientCacheOptions
{
    /// <summary>
    /// Overrides <see cref="ClientCredentialsTokenManagementOptions.UseCacheAutoTuning"/> for this client.
    /// When null, the global setting is used.
    ///
    /// Whether to use the cache auto-tuning feature, which tries to use the actual expiration time of the
    /// token to set the cache entry lifetime. The first time an access token is requested, its actual
    /// expiration time is not yet known, so the default cache lifetime is used.
    /// </summary>
    public bool? UseCacheAutoTuning { get; set; }

    /// <summary>
    /// The cache lifetime used for this client when no auto-tuned lifetime is available: always when
    /// auto-tuning is off, and for the first token request when it is on.
    /// When null, <see cref="ClientCredentialsTokenManagementOptions.DefaultCacheLifetime"/> is used.
    /// </summary>
    public TimeSpan? DefaultCacheLifetime { get; set; }

    /// <summary>
    /// Overrides <see cref="ClientCredentialsTokenManagementOptions.CacheLifetimeBuffer"/> for this client.
    /// When null, the global setting is used.
    ///
    /// The number of seconds to subtract from the token lifetime for the cache entry lifetime, and before
    /// which a cached token is treated as expired. This uses the same unit (seconds) as the global option.
    /// </summary>
    public int? CacheLifetimeBuffer { get; set; }

    /// <summary>
    /// Overrides <see cref="ClientCredentialsTokenManagementOptions.LocalCacheExpiration"/> for this client.
    /// When null, the global setting is used.
    ///
    /// How long the local cache for this client's tokens is valid. After this period the token is retrieved
    /// from the remote cache. To keep the local copy for as long as the remote cache entry for this client,
    /// set a value at least as large as the remote cache lifetime, such as <see cref="TimeSpan.MaxValue"/>.
    /// The local copy never outlives the overall cache expiration.
    /// </summary>
    public TimeSpan? LocalCacheExpiration { get; set; }
}
