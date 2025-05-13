// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.



namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

internal class OpenIdConnectUserAccessTokenRetriever(
    IUserAccessor userAccessor,
    IUserTokenManagementService userTokenManagement,
    UserTokenRequestParameters? parameters = null
) : AccessTokenRequestHandler.ITokenRetriever
{
    private readonly UserTokenRequestParameters _parameters = parameters ?? new UserTokenRequestParameters();

    public async Task<TokenResult<AccessTokenRequestHandler.IToken>> GetToken(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var parameters = new UserTokenRequestParameters
        {
            SignInScheme = _parameters.SignInScheme,
            ChallengeScheme = _parameters.ChallengeScheme,
            Resource = _parameters.Resource,
            Context = _parameters.Context,
        };

        var user = await userAccessor.GetCurrentUserAsync().ConfigureAwait(false);

        var getTokenResult = await userTokenManagement.GetAccessTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);
        if (getTokenResult.WasSuccessful(out var token, out var error))
        {
            return token;
        }

        return error;
    }
}
