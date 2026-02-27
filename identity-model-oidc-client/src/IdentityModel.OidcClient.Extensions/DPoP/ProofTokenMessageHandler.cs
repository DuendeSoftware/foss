// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;

namespace Duende.IdentityModel.OidcClient.DPoP;

/// <summary>
/// Message handler to create and send DPoP proof tokens.
/// </summary>
public class ProofTokenMessageHandler : DelegatingHandler
{
    private readonly IDPoPProofTokenFactory _proofTokenFactory;
    private string? _nonce;

    /// <summary>
    /// Constructor
    /// </summary>
    public ProofTokenMessageHandler(IDPoPProofTokenFactory dPoPProofTokenFactory, HttpMessageHandler innerHandler)
    {
        _proofTokenFactory = dPoPProofTokenFactory ?? throw new ArgumentNullException(nameof(dPoPProofTokenFactory));
        InnerHandler = innerHandler ?? throw new ArgumentNullException(nameof(innerHandler));
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CreateProofToken(request);

        var response = await base.SendAsync(request, cancellationToken);

        var dPoPNonce = response.GetDPoPNonce();

        if (dPoPNonce != _nonce)
        {
            // nonce is different, so hold onto it
            _nonce = dPoPNonce;

            // failure and nonce was different so we retry
            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();

                // If a ClientAssertionFactory was stored on the request
                // options, invoke it now to get a fresh assertion (new jti/iat)
                // for the retry attempt — servers that enforce assertion
                // uniqueness reject retries that reuse the same assertion JWT.
                if (request.Options.TryGetValue(ProtocolRequestOptions.ClientAssertionFactory, out var factory) && factory != null)
                {
                    await RefreshClientAssertionAsync(request, factory).ConfigureAwait(false);
                }

                CreateProofToken(request);

                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        return response;
    }

    /// <summary>
    /// Replaces the <c>client_assertion</c> and <c>client_assertion_type</c> form fields
    /// in the request body with a fresh assertion obtained from <paramref name="factory"/>.
    /// </summary>
    private static async Task RefreshClientAssertionAsync(HttpRequestMessage request, Func<Task<ClientAssertion>> factory)
    {
        var freshAssertion = await factory().ConfigureAwait(false);
        if (freshAssertion?.Value == null)
        {
            return;
        }

        // Read the existing form body.  FormUrlEncodedContent buffers internally,
        // so ReadAsStringAsync() is safe to call even after the first send.
        var body = request.Content != null
            ? await request.Content.ReadAsStringAsync().ConfigureAwait(false)
            : string.Empty;
        var parsed = System.Web.HttpUtility.ParseQueryString(body);

        // Replace client_assertion / client_assertion_type, or append if absent.
        parsed[OidcConstants.TokenRequest.ClientAssertionType] = freshAssertion.Type;
        parsed[OidcConstants.TokenRequest.ClientAssertion] = freshAssertion.Value;

        var pairs = parsed.AllKeys
            .Where(k => k != null)
            .SelectMany(k => parsed.GetValues(k)!.Select(v => new KeyValuePair<string, string>(k!, v)));

        request.Content = new FormUrlEncodedContent(pairs);
    }

    private void CreateProofToken(HttpRequestMessage request)
    {
        var proofRequest = new DPoPProofRequest
        {
            Method = request.Method.ToString(),
            Url = request.GetDPoPUrl(),
            DPoPNonce = _nonce
        };

        if (request.Headers.Authorization != null &&
            OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP.Equals(request.Headers.Authorization.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            proofRequest.AccessToken = request.Headers.Authorization.Parameter;
        }

        var proof = _proofTokenFactory.CreateProofToken(proofRequest);

        request.SetDPoPProofToken(proof.ProofToken);
    }
}
