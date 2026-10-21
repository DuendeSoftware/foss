// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Internal;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

// This class is to be configured as IOptions. Therefor, all properties have to be nullable and it must have a default constructor
// to make safe, I added a validator. 


/// <summary>
/// Defines a client credentials client
/// </summary>
public sealed class ClientCredentialsClient
{
    /// <summary>
    /// The authority used to discover the token endpoint when <see cref="TokenEndpoint"/> is not configured.
    /// </summary>
    /// <remarks>
    /// A full discovery document URL is also accepted when it ends with the default discovery path
    /// or <see cref="DiscoveryPolicy.DiscoveryDocumentPath"/>.
    /// Discovery is performed lazily, and the resolved endpoint is shared for the lifetime of these options.
    /// </remarks>
    public string? Authority { get; set; }

    /// <summary>
    /// The policy used to retrieve and validate the discovery document.
    /// </summary>
    /// <remarks>
    /// Defaults require HTTPS except on loopback addresses, validate the issuer and endpoints, and require a signing key set.
    /// </remarks>
    public DiscoveryPolicy DiscoveryPolicy { get; set; } = new();

    /// <summary>
    /// The address of the token endpoint. When configured, <see cref="Authority"/> is ignored.
    /// </summary>
    public Uri? TokenEndpoint { get; set; }

    /// <summary>
    /// The client ID
    /// </summary>
    public ClientId? ClientId { get; set; }

    /// <summary>
    /// The static (shared) client secret
    /// </summary>
    public ClientSecret? ClientSecret { get; set; }

    /// <summary>
    /// The client credential transmission style
    /// </summary>
    public ClientCredentialStyle ClientCredentialStyle { get; set; }

    /// <summary>
    /// Gets or sets the basic authentication header style (classic HTTP vs OAuth 2).
    /// </summary>
    /// <value>
    /// The basic authentication header style.
    /// </value>
    public BasicAuthenticationHeaderStyle AuthorizationHeaderStyle { get; set; } = BasicAuthenticationHeaderStyle.Rfc6749;

    /// <summary>
    /// The scope
    /// </summary>
    public Scope? Scope { get; set; }

    /// <summary>
    /// The resource
    /// </summary>
    public Resource? Resource { get; set; }

    /// <summary>
    /// The HTTP client name to use for the backchannel operations, will fall back to the standard backchannel client if
    /// not set
    /// </summary>
    public string? HttpClientName { get; set; }

    /// <summary>
    /// Additional parameters to send with token requests.
    /// </summary>
    public Parameters Parameters { get; set; } = [];

    /// <summary>
    /// The HTTP client instance to use for the back-channel operations, will override the HTTP client name if set
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// The string representation of the JSON web key to use for DPoP.
    /// </summary>
    public DPoPProofKey? DPoPJsonWebKey { get; set; }

    internal AsyncLazy<Uri>? DiscoveredTokenEndpoint { get; set; }

    internal HttpClient GetHttpClient(IHttpClientFactory httpClientFactory) =>
        HttpClient ?? httpClientFactory.CreateClient(
            string.IsNullOrWhiteSpace(HttpClientName)
                ? ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName
                : HttpClientName);

    internal sealed class Validator : IValidateOptions<ClientCredentialsClient>
    {
        public ValidateOptionsResult Validate(string? name, ClientCredentialsClient options)
        {
            if (options.ClientId is not null && (!string.IsNullOrWhiteSpace(options.Authority) || options.TokenEndpoint is not null))
            {
                return ValidateOptionsResult.Success;
            }

            var subject = options.ClientId != null
                ? "clientId " + options.ClientId
                : "client " + (name ?? "default");

            var errors = new List<string>();

            if (options.ClientId is null)
            {
                errors.Add($"No {nameof(options.ClientId)} configured for {subject}");
            }

            if (string.IsNullOrWhiteSpace(options.Authority) && options.TokenEndpoint is null)
            {
                errors.Add($"No {nameof(options.Authority)} or {nameof(options.TokenEndpoint)} configured for {subject}");
            }

            return ValidateOptionsResult.Fail(errors);
        }
    }
}
