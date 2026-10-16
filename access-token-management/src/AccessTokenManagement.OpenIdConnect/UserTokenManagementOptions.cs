// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Duende.AccessTokenManagement.DPoP;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Options for user access token management
/// </summary>
public sealed class UserTokenManagementOptions
{
    /// <summary>
    /// Name of the authentication scheme to use for deriving token service configuration
    /// (will fall back to configured default challenge scheme if not set)
    /// </summary>
    public Scheme? ChallengeScheme { get; set; }

    /// <summary>
    /// Boolean to set whether tokens added to a session should be challenge-scheme-specific.
    /// The default is 'false'.
    /// </summary>
    public bool UseChallengeSchemeScopedTokens { get; set; }

    /// <summary>
    /// Timespan that specifies how long before expiration, the token should be refreshed (defaults to 1 minute)
    /// </summary>
    public TimeSpan RefreshBeforeExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Scope value when requesting a client credentials token.
    /// If not set, token request will omit scope parameter.
    /// </summary>
    public Scope? ClientCredentialsScope { get; set; }

    /// <summary>
    /// Resource value when requesting a client credentials token.
    /// If not set, token request will omit resource parameter.
    /// </summary>
    public Resource? ClientCredentialsResource { get; set; }

    /// <summary>
    /// Default client credential style to use when requesting tokens
    /// </summary>
    public ClientCredentialStyle ClientCredentialStyle { get; set; } = ClientCredentialStyle.AuthorizationHeader;
    // TODO: should we make this same as the AspNet OIDC handler (which defaults to the form body)

    /// <summary>
    /// The string representation of the JSON web key to use for DPoP.
    /// </summary>
    public DPoPProofKey? DPoPJsonWebKey { get; set; }

    /// <summary>
    /// The maximum accepted length of user access tokens. The limit is applied to token endpoint refresh
    /// responses and to tokens read back from the authentication session. The value must be greater than zero.
    /// The default is <see cref="AccessToken.MaxLength"/>. By default, user tokens are stored in the
    /// authentication cookie or session, so raising this limit can produce a larger cookie or session.
    /// Server or proxy header size limits may apply. Server-side sessions reduce cookie size.
    /// </summary>
    public int MaxAccessTokenLength { get; set; } = AccessToken.MaxLength;

    /// <summary>
    /// The maximum accepted length of user refresh tokens. The limit is applied to token endpoint refresh
    /// responses and to tokens read back from the authentication session. The value must be greater than zero.
    /// The default is <see cref="RefreshToken.MaxLength"/>. By default, user tokens are stored in the
    /// authentication cookie or session, so raising this limit can produce a larger cookie or session.
    /// Server or proxy header size limits may apply. Server-side sessions reduce cookie size.
    /// </summary>
    public int MaxRefreshTokenLength { get; set; } = RefreshToken.MaxLength;

    /// <summary>
    /// The maximum accepted length of user identity tokens. The limit is applied to token endpoint refresh
    /// responses and to tokens read back from the authentication session. The value must be greater than zero.
    /// The default is <see cref="IdentityToken.MaxLength"/>. By default, user tokens are stored in the
    /// authentication cookie or session, so raising this limit can produce a larger cookie or session.
    /// Server or proxy header size limits may apply. Server-side sessions reduce cookie size.
    /// </summary>
    public int MaxIdentityTokenLength { get; set; } = IdentityToken.MaxLength;

    internal sealed class Validator : IValidateOptions<UserTokenManagementOptions>
    {
        public ValidateOptionsResult Validate(string? name, UserTokenManagementOptions options)
        {
            var errors = new List<string>();

            if (options.MaxAccessTokenLength <= 0)
            {
                errors.Add($"{nameof(UserTokenManagementOptions)}.{nameof(MaxAccessTokenLength)} must be greater than zero.");
            }

            if (options.MaxRefreshTokenLength <= 0)
            {
                errors.Add($"{nameof(UserTokenManagementOptions)}.{nameof(MaxRefreshTokenLength)} must be greater than zero.");
            }

            if (options.MaxIdentityTokenLength <= 0)
            {
                errors.Add($"{nameof(UserTokenManagementOptions)}.{nameof(MaxIdentityTokenLength)} must be greater than zero.");
            }

            return errors.Count > 0
                ? ValidateOptionsResult.Fail(errors)
                : ValidateOptionsResult.Success;
        }
    }
}
