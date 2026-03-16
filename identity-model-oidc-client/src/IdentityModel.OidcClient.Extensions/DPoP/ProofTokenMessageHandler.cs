// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Web;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityModel.OidcClient.DPoP;

/// <summary>
/// Message handler to create and send DPoP proof tokens.
/// </summary>
public class ProofTokenMessageHandler : DelegatingHandler
{
    private readonly IDPoPProofTokenFactory _proofTokenFactory;
    private readonly ILogger _logger;
    private string? _nonce;

    /// <summary>
    /// Constructor
    /// </summary>
    public ProofTokenMessageHandler(IDPoPProofTokenFactory dPoPProofTokenFactory, HttpMessageHandler innerHandler)
        : this(dPoPProofTokenFactory, innerHandler, NullLogger<ProofTokenMessageHandler>.Instance)
    {
    }

    /// <summary>
    /// Constructor with logger support
    /// </summary>
    public ProofTokenMessageHandler(IDPoPProofTokenFactory dPoPProofTokenFactory, HttpMessageHandler innerHandler, ILogger<ProofTokenMessageHandler> logger)
    {
        _proofTokenFactory = dPoPProofTokenFactory ?? throw new ArgumentNullException(nameof(dPoPProofTokenFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                CreateProofToken(request);

                // Regenerate the client assertion to ensure a fresh jti on retry.
                await RefreshClientAssertionAsync(request).ConfigureAwait(false);

                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        return response;
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

    /// <summary>
    /// Reads the <see cref="ProtocolRequestOptions.ClientAssertionFactory"/> from
    /// <see cref="HttpRequestMessage.Options"/> and, if present, invokes it to obtain a
    /// fresh <see cref="ClientAssertion"/>. The assertion replaces the
    /// <c>client_assertion</c> and <c>client_assertion_type</c> form fields in the
    /// request body while preserving all other parameters.
    /// </summary>
    private async Task RefreshClientAssertionAsync(HttpRequestMessage request)
    {
        if (request.Content == null)
        {
            return;
        }

        if (!request.Options.TryGetValue(ProtocolRequestOptions.ClientAssertionFactory, out var factory) ||
            factory == null)
        {
            return;
        }

        ClientAssertion? assertion;
        try
        {
            assertion = await factory().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // If the factory throws, leave the body unchanged and let the retry proceed.
            _logger.LogWarning(ex, "Client assertion factory threw an exception during DPoP nonce retry. " +
                                   "The retry will proceed with the original assertion.");
            return;
        }

        if (assertion == null)
        {
            return;
        }

        string bodyString;
        try
        {
            bodyString = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body while refreshing client assertion during DPoP nonce retry. " +
                                   "The retry will proceed with the original assertion.");
            return;
        }

        // Parse the application/x-www-form-urlencoded body.
        var parsed = HttpUtility.ParseQueryString(bodyString);

        parsed[OidcConstants.TokenRequest.ClientAssertionType] = assertion.Type;
        parsed[OidcConstants.TokenRequest.ClientAssertion] = assertion.Value;

        request.Content = new StringContent(
            parsed.ToString()!,
            System.Text.Encoding.UTF8,
            "application/x-www-form-urlencoded");
    }
}
