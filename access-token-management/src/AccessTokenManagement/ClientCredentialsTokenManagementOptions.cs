// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Client access token options
/// </summary>
public class ClientCredentialsTokenManagementOptions
{
    /// <summary>
    /// Used to prefix the cache key
    /// </summary>
    public string CacheKeyPrefix { get; set; } = "Duende.AccessTokenManagement.Cache::";

    /// <summary>
    /// Prefix used for the nonce store key
    /// </summary>
    public string NonceStoreKeyPrefix { get; set; } = "Duende.AccessTokenManagement.DPoPNonceStore::";

    /// <summary>
    /// Value to subtract from token lifetime for the cache entry lifetime (defaults to 60 seconds)
    /// </summary>
    public int CacheLifetimeBuffer { get; set; } = 60;

    /// <summary>
    /// By default, access token management uses <see cref="AccessTokenHandler"/>. Enabling this preview flag will replace this
    /// with a more extensible model that allows you to use your own <see cref="ITokenRetriever{TToken}"/> and <see cref="ISendRequestRetryPolicy"/>
    /// It switches to use <see cref="AccessTokenHandler{TTokenRetriever,TToken}"/>
    /// 
    /// In future versions, this will be the default behavior.
    /// </summary>
    public bool UsePreviewExtensibilityOnAccessTokenHandlers { get; set; }

}
