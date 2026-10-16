// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

internal sealed class PostConfigureClientCredentialsClient(IHttpClientFactory httpClientFactory)
    : IPostConfigureOptions<ClientCredentialsClient>
{
    public void PostConfigure(string? name, ClientCredentialsClient options)
    {
        if (options.TokenEndpoint is null && !string.IsNullOrWhiteSpace(options.Authority))
        {
            options.DiscoveredTokenEndpoint = new AsyncLazy<Uri>(() => GetTokenEndpointFromDiscoveryDocumentAsync(options));
        }
    }

    private async Task<Uri> GetTokenEndpointFromDiscoveryDocumentAsync(ClientCredentialsClient options)
    {
        var client = ClientCredentialsTokenClient.GetHttpClient(options, httpClientFactory);

        using var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = options.Authority,
            Policy = options.DiscoveryPolicy
        }).ConfigureAwait(false);

        if (disco.IsError)
        {
            var message = disco.ErrorType switch
            {
                ResponseErrorType.Http => $"Discovery endpoint {options.Authority} is unavailable: {disco.Error}",
                ResponseErrorType.PolicyViolation => $"Policy error while contacting the discovery endpoint {options.Authority}: {disco.Error}",
                ResponseErrorType.Exception => $"Error parsing discovery document from {options.Authority}: {disco.Error}",
                _ => $"Error retrieving discovery document from {options.Authority}: {disco.Error}"
            };

            throw new InvalidOperationException(message, disco.Exception);
        }

        if (string.IsNullOrWhiteSpace(disco.TokenEndpoint))
        {
            throw new InvalidOperationException($"No token endpoint found in discovery document for client {options.ClientId}");
        }

        return new Uri(disco.TokenEndpoint, UriKind.Absolute);
    }
}
