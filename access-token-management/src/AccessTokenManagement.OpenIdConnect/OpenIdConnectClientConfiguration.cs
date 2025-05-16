// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.



namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Configuration setting sourced from the OpenID Connect handler
/// </summary>
public sealed class OpenIdConnectClientConfiguration
{
    ///// <summary>
    ///// The authority
    ///// </summary>
    //public Authority? Authority { get; set; }

    /// <summary>
    /// The token endpoint address
    /// </summary>
    public required Uri TokenEndpoint { get; set; }

    /// <summary>
    /// The revocation endpoint address
    /// </summary>
    public Uri? RevocationEndpoint { get; set; }

    /// <summary>
    /// The client ID
    /// </summary>
    public required ClientId ClientId { get; set; }

    /// <summary>
    /// The client secret
    /// </summary>
    public required ClientSecret ClientSecret { get; set; }

    /// <summary>
    /// The HTTP client associated with the OIDC handler (if based on scheme configuration)
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// The scheme name of the OIDC handler
    /// </summary>
    public Scheme? Scheme { get; set; }

}
