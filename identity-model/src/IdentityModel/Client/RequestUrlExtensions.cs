// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Internal;

namespace Duende.IdentityModel.Client;

/// <summary>
/// Extensions for RequestUrl
/// </summary>
public static class RequestUrlExtensions
{
    /// <summary>
    /// Creates an authorize URL.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns></returns>
    public static string Create(this RequestUrl request, Parameters parameters) => request.Create(parameters);

    /// <summary>
    /// Creates an authorize URL.
    /// </summary>
    /// <param name="request">The instance of the RequestUrl helper class.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="responseType">The response type.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="redirectUri">The redirect URI.</param>
    /// <param name="state">The state.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="loginHint">The login hint.</param>
    /// <param name="acrValues">The acr values.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="responseMode">The response mode.</param>
    /// <param name="codeChallenge">The code challenge.</param>
    /// <param name="codeChallengeMethod">The code challenge method.</param>
    /// <param name="display">The display option.</param>
    /// <param name="maxAge">The max age.</param>
    /// <param name="uiLocales">The ui locales.</param>
    /// <param name="idTokenHint">The id_token hint.</param>
    /// <param name="requestUri">The request uri.</param>
    /// <param name="extra">Extra parameters.</param>
    /// <returns></returns>
    public static string CreateAuthorizeUrl(this RequestUrl request,
        string clientId,
        string? responseType = null,
        string? scope = null,
        string? redirectUri = null,
        string? state = null,
        string? nonce = null,
        string? loginHint = null,
        string? acrValues = null,
        string? prompt = null,
        string? responseMode = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        string? display = null,
        int? maxAge = null,
        string? uiLocales = null,
        string? idTokenHint = null,
        string? requestUri = null,
        Parameters? extra = null)
    {
        var values = new Parameters
        {
            { OidcConstants.AuthorizeRequest.ClientId, clientId },
        };

        if (requestUri.IsPresent())
        {
            values.AddRequired(OidcConstants.AuthorizeRequest.RequestUri, requestUri);
        }
        else
        {
            values.AddRequired(OidcConstants.AuthorizeRequest.ResponseType, responseType);
            values.AddOptional(OidcConstants.AuthorizeRequest.Scope, scope);
            values.AddOptional(OidcConstants.AuthorizeRequest.RedirectUri, redirectUri);
            values.AddOptional(OidcConstants.AuthorizeRequest.State, state);
            values.AddOptional(OidcConstants.AuthorizeRequest.Nonce, nonce);
            values.AddOptional(OidcConstants.AuthorizeRequest.LoginHint, loginHint);
            values.AddOptional(OidcConstants.AuthorizeRequest.AcrValues, acrValues);
            values.AddOptional(OidcConstants.AuthorizeRequest.Prompt, prompt);
            values.AddOptional(OidcConstants.AuthorizeRequest.ResponseMode, responseMode);
            values.AddOptional(OidcConstants.AuthorizeRequest.CodeChallenge, codeChallenge);
            values.AddOptional(OidcConstants.AuthorizeRequest.CodeChallengeMethod, codeChallengeMethod);
            values.AddOptional(OidcConstants.AuthorizeRequest.Display, display);
            values.AddOptional(OidcConstants.AuthorizeRequest.MaxAge, maxAge?.ToString());
            values.AddOptional(OidcConstants.AuthorizeRequest.UiLocales, uiLocales);
            values.AddOptional(OidcConstants.AuthorizeRequest.IdTokenHint, idTokenHint);
            values.AddOptional(OidcConstants.AuthorizeRequest.RequestUri, requestUri);
        }

        return request.Create(values.Merge(extra));
    }

    /// <summary>
    /// Creates a end_session URL.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="idTokenHint">The id_token hint.</param>
    /// <param name="postLogoutRedirectUri">The post logout redirect URI.</param>
    /// <param name="state">The state.</param>
    /// <param name="extra">The extra parameters.</param>
    /// <returns></returns>
    public static string CreateEndSessionUrl(this RequestUrl request,
        string? idTokenHint = null,
        string? postLogoutRedirectUri = null,
        string? state = null,
        Parameters? extra = null)
    {
        var values = new Parameters();

        values.AddOptional(OidcConstants.EndSessionRequest.IdTokenHint, idTokenHint);
        values.AddOptional(OidcConstants.EndSessionRequest.PostLogoutRedirectUri, postLogoutRedirectUri);
        values.AddOptional(OidcConstants.EndSessionRequest.State, state);

        return request.Create(values.Merge(extra));
    }
}
