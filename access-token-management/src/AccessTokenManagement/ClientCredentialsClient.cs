// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
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
    /// The address of the token endpoint
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
    /// The HTTP client name to use for the backchannel operations, will fall back to the standard backchannel client if not set
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


    internal sealed class Validator : IValidateOptions<ClientCredentialsClient>
    {
        public ValidateOptionsResult Validate(string? name, ClientCredentialsClient options)
        {
            var subject = options.ClientId != null
                ? "clientId " + options.ClientId
                : "client " + (name ?? "default");

            var errors = new List<string>();

            if (options.ClientId == null)
            {
                errors.Add($"No {nameof(options.ClientId)} configured for {subject}");
            }

            if (options.TokenEndpoint == null)
            {
                errors.Add($"{nameof(options.TokenEndpoint)} cannot be null for {subject}");
            }

            if (errors.Any())
            {
                return ValidateOptionsResult.Fail(errors);
            }
            return ValidateOptionsResult.Success;
        }
    }
}
