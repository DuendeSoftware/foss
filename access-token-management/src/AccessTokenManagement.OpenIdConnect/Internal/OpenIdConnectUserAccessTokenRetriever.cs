// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.



namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

internal class OpenIdConnectUserAccessTokenRetriever(
    IUserAccessor userAccessor,
    IUserTokenManager userTokenManagement,
    UserTokenRequestParameters? parameters = null
) : AccessTokenRequestHandler.ITokenRetriever
{
    private readonly UserTokenRequestParameters _parameters = parameters ?? new UserTokenRequestParameters();

    public async Task<TokenResult<AccessTokenRequestHandler.IToken>> GetTokenAsync(HttpRequestMessage request, CT ct)
    {
        var parameters = new UserTokenRequestParameters
        {
            SignInScheme = _parameters.SignInScheme,
            ChallengeScheme = _parameters.ChallengeScheme,
            Resource = _parameters.Resource,
            Context = _parameters.Context,
        };

        var user = await userAccessor.GetCurrentUserAsync(ct).ConfigureAwait(false);

        var getTokenResult = await userTokenManagement.GetAccessTokenAsync(user, parameters, ct).ConfigureAwait(false);
        if (getTokenResult.WasSuccessful(out var token, out var error))
        {
            return token;
        }

        return error;
    }
}
