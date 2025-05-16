// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel.Internal;
using Duende.IdentityModel.Jwk;

namespace Duende.IdentityModel.Client;

/// <summary>
/// Represents the response from an OpenID Connect discovery endpoint.
/// </summary>
public class DiscoveryDocumentResponse : ProtocolResponse
{
    /// <summary>
    /// Gets or sets the discovery policy used to configure how the discovery document is processed.
    /// </summary>
    /// <value>
    /// An instance of <see cref="DiscoveryPolicy"/> that represents the processing configuration.
    /// </value>
    public DiscoveryPolicy Policy { get; set; } = default!;

    /// <summary>
    /// Initializes the discovery document response using the provided data.
    /// </summary>
    /// <param name="initializationData">The data used to initialize the response, typically a DiscoveryPolicy object.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override Task InitializeAsync(object? initializationData = null)
    {
        if (HttpResponse?.IsSuccessStatusCode != true)
        {
            ErrorMessage = initializationData as string;
            return Task.CompletedTask;
        }

        Policy = initializationData as DiscoveryPolicy ?? new DiscoveryPolicy();

        var validationError = Validate(Policy);

        if (validationError.IsPresent())
        {
            Json = default;

            ErrorType = ResponseErrorType.PolicyViolation;
            ErrorMessage = validationError;
        }

        MtlsEndpointAliases =
            new MtlsEndpointAliases(Json?.TryGetValue(OidcConstants.Discovery.MtlsEndpointAliases));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets or sets the JSON Web Key Set (JWKS) associated with the discovery document.
    /// </summary>
    /// <value>
    /// An instance of <see cref="JsonWebKeySet"/> that contains the public keys used for validating signatures and encryption.
    /// </value>
    public JsonWebKeySet? KeySet { get; set; }

    /// <summary>
    /// Gets the mutual TLS (mTLS) endpoint aliases.
    /// </summary>
    /// <value>
    /// An instance of <see cref="MtlsEndpointAliases"/> that contains the mTLS endpoint aliases.
    /// </value>
    public MtlsEndpointAliases? MtlsEndpointAliases { get; internal set; }

    // strongly typed
    /// <summary>
    /// Gets the issuer identifier for the authorization server.
    /// </summary>
    /// <value>
    /// The issuer URL as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? Issuer => TryGetString(OidcConstants.Discovery.Issuer);

    /// <summary>
    /// Gets the authorization endpoint URL.
    /// </summary>
    /// <value>
    /// The authorization endpoint URL as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? AuthorizeEndpoint => TryGetString(OidcConstants.Discovery.AuthorizationEndpoint);

    /// <summary>
    /// Gets token endpoint URL.
    /// </summary>
    /// <value>
    /// The token endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? TokenEndpoint => TryGetString(OidcConstants.Discovery.TokenEndpoint);

    /// <summary>
    /// Gets user info endpoint URL.
    /// </summary>
    /// <value>
    /// The user info endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? UserInfoEndpoint => TryGetString(OidcConstants.Discovery.UserInfoEndpoint);

    /// <summary>
    /// Gets the introspection endpoint URL.
    /// </summary>
    /// <value>
    /// The introspection endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? IntrospectionEndpoint => TryGetString(OidcConstants.Discovery.IntrospectionEndpoint);

    /// <summary>
    /// Gets the revocation endpoint URL.
    /// </summary>
    /// <value>
    /// The revocation endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? RevocationEndpoint => TryGetString(OidcConstants.Discovery.RevocationEndpoint);

    /// <summary>
    /// Gets the device authorization endpoint URL.
    /// </summary>
    /// <value>
    /// The device authorization endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? DeviceAuthorizationEndpoint => TryGetString(OidcConstants.Discovery.DeviceAuthorizationEndpoint);

    /// <summary>
    /// Gets the backchannel authentication endpoint URL.
    /// </summary>
    /// <value>
    /// The backchannel authentication endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? BackchannelAuthenticationEndpoint => TryGetString(OidcConstants.Discovery.BackchannelAuthenticationEndpoint);

    /// <summary>
    /// Gets the URI of the JSON Web Key Set (JWKS).
    /// </summary>
    /// <value>
    /// The JWKS URI as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? JwksUri => TryGetString(OidcConstants.Discovery.JwksUri);

    /// <summary>
    /// Gets the end session endpoint URL.
    /// </summary>
    /// <value>
    /// The end session endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? EndSessionEndpoint => TryGetString(OidcConstants.Discovery.EndSessionEndpoint);

    /// <summary>
    /// Gets the check session iframe URL.
    /// </summary>
    /// <value>
    /// The check session iframe as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? CheckSessionIframe => TryGetString(OidcConstants.Discovery.CheckSessionIframe);

    /// <summary>
    /// Gets the dynamic client registration (DCR) endpoint URL.
    /// </summary>
    /// <value>
    /// The DCR endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? RegistrationEndpoint => TryGetString(OidcConstants.Discovery.RegistrationEndpoint);

    /// <summary>
    /// Gets the pushed authorization request (PAR) endpoint URL.
    /// </summary>
    /// <value>
    /// The PAR endpoint as a string, or <c>null</c> if it is not found in the discovery document.
    /// </value>
    public string? PushedAuthorizationRequestEndpoint => TryGetString(OidcConstants.Discovery.PushedAuthorizationRequestEndpoint);

    /// <summary>
    /// Gets a flag indicating whether front-channel logout is supported.
    /// </summary>
    /// <value>
    /// <c>true</c> if front-channel logout is supported, <c>false</c> if it is explicitly not supported, or
    /// <c>null</c> if it is not found in the discovery document.
    /// </value>
    public bool? FrontChannelLogoutSupported => TryGetBoolean(OidcConstants.Discovery.FrontChannelLogoutSupported);

    /// <summary>
    /// Gets a flag indicating whether a session ID (sid) parameter is supported at the front-channel logout endpoint.
    /// </summary>
    /// <value>
    /// <c>true</c> if the sid parameter is supported at the front-channel logout endpoint, <c>false</c> if it is explicitly not supported, or
    /// <c>null</c> if it is not found in the discovery document.
    /// </value>
    public bool? FrontChannelLogoutSessionSupported => TryGetBoolean(OidcConstants.Discovery.FrontChannelLogoutSessionSupported);

    /// <summary>
    /// Gets the supported grant types.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported grant types.
    /// </value>
    public IEnumerable<string> GrantTypesSupported => TryGetStringArray(OidcConstants.Discovery.GrantTypesSupported);

    /// <summary>
    /// Gets the supported code challenge methods.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported code challenge methods, such as "S256".
    /// </value>
    public IEnumerable<string> CodeChallengeMethodsSupported => TryGetStringArray(OidcConstants.Discovery.CodeChallengeMethodsSupported);

    /// <summary>
    /// Gets the supported scopes.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported scopes.
    /// </value>
    public IEnumerable<string> ScopesSupported => TryGetStringArray(OidcConstants.Discovery.ScopesSupported);

    /// <summary>
    /// Gets the supported subject types.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported subject types, e.g., "public" and "pairwise".
    /// </value>
    public IEnumerable<string> SubjectTypesSupported => TryGetStringArray(OidcConstants.Discovery.SubjectTypesSupported);

    /// <summary>
    /// Gets the supported response modes.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported response modes.
    /// </value>
    public IEnumerable<string> ResponseModesSupported => TryGetStringArray(OidcConstants.Discovery.ResponseModesSupported);

    /// <summary>
    /// Gets the supported response types.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported response types.
    /// </value>
    public IEnumerable<string> ResponseTypesSupported => TryGetStringArray(OidcConstants.Discovery.ResponseTypesSupported);

    /// <summary>
    /// Gets the supported claims.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported claims.
    /// </value>
    public IEnumerable<string> ClaimsSupported => TryGetStringArray(OidcConstants.Discovery.ClaimsSupported);

    /// <summary>
    /// Gets the authentication methods supported by the token endpoint.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported authentication methods.
    /// </value>
    public IEnumerable<string> TokenEndpointAuthenticationMethodsSupported => TryGetStringArray(OidcConstants.Discovery.TokenEndpointAuthenticationMethodsSupported);

    /// <summary>
    /// Gets the signing algorithms supported by the token endpoint for the signature on the JWT used to authenticate
    /// the client at the token endpoint for the "private_key_jwt" and "client_secret_jwt" authentication methods.
    /// </summary>
    public IEnumerable<string> TokenEndpointAuthenticationSigningAlgorithmsSupported => [];


    /// <summary>
    /// Gets the supported backchannel token delivery modes.
    /// </summary>
    /// <value>
    /// A collection of strings representing the supported backchannel token delivery modes.
    /// </value>
    public IEnumerable<string> BackchannelTokenDeliveryModesSupported => TryGetStringArray(OidcConstants.Discovery.BackchannelTokenDeliveryModesSupported);

    /// <summary>
    /// Gets a flag indicating whether the backchannel user code parameter is supported.
    /// </summary>
    /// <value>
    /// <c>true</c> if the backchannel user code parameter is supported, <c>false</c> if it is explicitly not supported, or
    /// <c>null</c> if it is not found in the discovery document.
    /// </value>
    public bool? BackchannelUserCodeParameterSupported => TryGetBoolean(OidcConstants.Discovery.BackchannelUserCodeParameterSupported);

    /// <summary>
    /// Gets a flag indicating whether the use of pushed authorization requests (PAR) is required.
    /// </summary>
    /// <value>
    /// <c>true</c> if the PAR is required, <c>false</c> if it is explicitly not required, or
    /// <c>null</c> if it is not found in the discovery document.
    /// </value>
    public bool? RequirePushedAuthorizationRequests => TryGetBoolean(OidcConstants.Discovery.RequirePushedAuthorizationRequests);

    /// <summary>
    /// Gets the signing algorithms supported for introspection responses.
    /// </summary>
    /// <value>
    /// A collection of algorithm identifier strings representing the supported signing algorithms for token introspection.
    /// </value>
    public IEnumerable<string> IntrospectionSigningAlgorithmsSupported =>
        TryGetStringArray(OidcConstants.Discovery.IntrospectionSigningAlgorithmsSupported);

    /// <summary>
    /// Gets the encryption "alg" values supported for encrypted JWT introspection responses.
    /// </summary>
    /// <value>
    /// A collection of algorithm identifier strings representing the supported encryption "alg" values.
    /// </value>
    public IEnumerable<string> IntrospectionEncryptionAlgorithmsSupported =>
        TryGetStringArray(OidcConstants.Discovery.IntrospectionEncryptionAlgorithmsSupported);

    /// <summary>
    /// Gets the encryption "enc" values supported for encrypted JWT introspection responses.
    /// </summary>
    /// <value>
    /// A collection of algorithm identifier strings representing the supported encryption "enc" values.
    /// </value>
    public IEnumerable<string> IntrospectionEncryptionEncValuesSupported =>
        TryGetStringArray(OidcConstants.Discovery.IntrospectionEncryptionEncValuesSupported);

    /// <summary>
    /// Attempts to retrieve a JSON value for a given property name from the discovery document.
    /// </summary>
    /// <param name="name">The name of the property whose value is to be retrieved.</param>
    /// <returns>A <see cref="JsonElement"/> containing the value if found, and <c>null</c> otherwise.</returns>
    public JsonElement? TryGetValue(string name) => Json?.TryGetValue(name);

    /// <summary>
    /// Attempts to retrieve a string value for a given property name from the discovery document.
    /// </summary>
    /// <param name="name">The name of the property whose value is to be retrieved.</param>
    /// <returns>The string value if found, and <c>null</c> otherwise.</returns>
    public string? TryGetString(string name) => Json?.TryGetString(name);

    /// <summary>
    /// Attempts to retrieve a boolean value for a given property name from the discovery document.
    /// </summary>
    /// <param name="name">The name of the property whose value is to be retrieved.</param>
    /// <returns>The boolean value if found, and <c>null</c> otherwise.</returns>
    public bool? TryGetBoolean(string name) => Json?.TryGetBoolean(name);

    /// <summary>
    /// Attempts to retrieve a string array for a given property name from the discovery document.
    /// </summary>
    /// <param name="name">The name of the property whose value is to be retrieved.</param>
    /// <returns>The collection of strings if found, and an empty collection otherwise.</returns>
    public IEnumerable<string> TryGetStringArray(string name) => Json?.TryGetStringArray(name) ?? [];

    private string Validate(DiscoveryPolicy policy)
    {
        if (policy.ValidateIssuerName)
        {
            var strategy = policy.AuthorityValidationStrategy ?? DiscoveryPolicy.DefaultAuthorityValidationStrategy;

            var issuerValidationResult = strategy.IsIssuerNameValid(Issuer!, policy.Authority);

            if (!issuerValidationResult.Success)
            {
                return issuerValidationResult.ErrorMessage;
            }
        }

        var error = ValidateEndpoints(Json, policy);
        if (error.IsPresent())
        {
            return error;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if the issuer matches the authority.
    /// </summary>
    /// <param name="issuer">The issuer.</param>
    /// <param name="authority">The authority.</param>
    /// <returns></returns>
    public bool ValidateIssuerName(string issuer, string authority) =>
        DiscoveryPolicy.DefaultAuthorityValidationStrategy.IsIssuerNameValid(issuer, authority).Success;

    /// <summary>
    /// Checks if the issuer matches the authority.
    /// </summary>
    /// <param name="issuer">The issuer.</param>
    /// <param name="authority">The authority.</param>
    /// <param name="nameComparison">The comparison mechanism that should be used when performing the match.</param>
    /// <returns></returns>
    public bool ValidateIssuerName(string issuer, string authority, StringComparison nameComparison) =>
        new StringComparisonAuthorityValidationStrategy(nameComparison).IsIssuerNameValid(issuer, authority).Success;

    /// <summary>
    /// Checks if the issuer matches the authority.
    /// </summary>
    /// <param name="issuer">The issuer.</param>
    /// <param name="authority">The authority.</param>
    /// <param name="validationStrategy">The strategy to use.</param>
    /// <returns></returns>
    private bool ValidateIssuerName(string issuer, string authority, IAuthorityValidationStrategy validationStrategy) =>
        validationStrategy.IsIssuerNameValid(issuer, authority).Success;

    /// <summary>
    /// Validates the endoints and jwks_uri according to the security policy.
    /// </summary>
    /// <param name="json">The json.</param>
    /// <param name="policy">The policy.</param>
    /// <returns></returns>
    public string ValidateEndpoints(JsonElement? json, DiscoveryPolicy policy)
    {
        if (json == null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        // allowed hosts
        var allowedHosts = new HashSet<string>(policy.AdditionalEndpointBaseAddresses.Select(e => new Uri(e).Authority))
        {
            new Uri(policy.Authority).Authority
        };

        // allowed authorities (hosts + base address)
        var allowedAuthorities = new HashSet<string>(policy.AdditionalEndpointBaseAddresses)
        {
            policy.Authority
        };

        // Can't actually be null, because we check that and throw at the beginning of the method
        foreach (var element in json?.EnumerateObject()!)
        {
            if (element.Name.EndsWith("endpoint", StringComparison.OrdinalIgnoreCase) ||
                element.Name.Equals(OidcConstants.Discovery.JwksUri, StringComparison.OrdinalIgnoreCase) ||
                element.Name.Equals(OidcConstants.Discovery.CheckSessionIframe, StringComparison.OrdinalIgnoreCase))
            {
                var endpoint = element.Value.ToString();

                var isValidUri = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri);
                if (!isValidUri)
                {
                    return $"Malformed endpoint: {endpoint}";
                }

                if (!DiscoveryEndpoint.IsValidScheme(uri!))
                {
                    return $"Malformed endpoint: {endpoint}";
                }

                if (!DiscoveryEndpoint.IsSecureScheme(uri!, policy))
                {
                    return $"Endpoint does not use HTTPS: {endpoint}";
                }

                if (policy.ValidateEndpoints)
                {
                    // if endpoint is on exclude list, don't validate
                    if (policy.EndpointValidationExcludeList.Contains(element.Name))
                    {
                        continue;
                    }

                    var isAllowed = false;
                    foreach (var host in allowedHosts)
                    {
                        if (string.Equals(host, uri!.Authority))
                        {
                            isAllowed = true;
                        }
                    }

                    if (!isAllowed)
                    {
                        return $"Endpoint is on a different host than authority: {endpoint}";
                    }

                    var strategy = policy.AuthorityValidationStrategy ?? DiscoveryPolicy.DefaultAuthorityValidationStrategy;
                    var endpointValidationResult = strategy.IsEndpointValid(endpoint, allowedAuthorities);
                    if (!endpointValidationResult.Success)
                    {
                        return endpointValidationResult.ErrorMessage;
                    }
                }
            }
        }

        if (policy.RequireKeySet)
        {
            if (string.IsNullOrWhiteSpace(JwksUri))
            {
                return "Keyset is missing";
            }
        }

        return string.Empty;
    }
}
