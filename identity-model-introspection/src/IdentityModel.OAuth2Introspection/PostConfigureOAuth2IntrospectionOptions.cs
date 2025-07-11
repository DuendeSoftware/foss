// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


<<<<<<< TODO: Unmerged change from project 'IdentityModel.AspNetCore.OAuth2Introspection(net9.0)', Before:
using System;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.AspNetCore.OAuth2Introspection.Infrastructure;
=======
using IdentityModel.AspNetCore.OAuth2Introspection.Infrastructure;
>>>>>>> After
using Duende.IdentityModel.Client;
using 
<<<<<<< TODO: Unmerged change from project 'IdentityModel.AspNetCore.OAuth2Introspection(net9.0)', Before:
namespace IdentityModel.AspNetCore.OAuth2Introspection
{
    internal class PostConfigureOAuth2IntrospectionOptions : IPostConfigureOptions<OAuth2IntrospectionOptions>
    {
        private readonly IDistributedCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;

        public PostConfigureOAuth2IntrospectionOptions(IHttpClientFactory httpClientFactory, IDistributedCache cache = null)
        {
            _cache = cache;
            _httpClientFactory = httpClientFactory;
        }

        public void PostConfigure(string name, OAuth2IntrospectionOptions options)
        {
            options.Validate();

            if (options.EnableCaching && _cache == null)
            {
                throw new ArgumentException("Caching is enabled, but no IDistributedCache is found in the services collection", nameof(_cache));
            }
            
            options.IntrospectionClient = new AsyncLazy<HttpClient>(() => InitializeIntrospectionClient(options));
        }

        private async Task<HttpClient> InitializeIntrospectionClient(OAuth2IntrospectionOptions options)
        {
            if (!options.IntrospectionEndpoint.IsPresent())
            {
                options.IntrospectionEndpoint = await GetIntrospectionEndpointFromDiscoveryDocument(options).ConfigureAwait(false);
            }

            return _httpClientFactory.CreateClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName);
        }

        private async Task<string> GetIntrospectionEndpointFromDiscoveryDocument(OAuth2IntrospectionOptions options)
        {
            var client = _httpClientFactory.CreateClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName);

            var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
            {
                Address = options.Authority,
                Policy = options?.DiscoveryPolicy ?? new DiscoveryPolicy()
            }).ConfigureAwait(false);
            
            if (disco.IsError)
            {
                switch (disco.ErrorType)
                {
                    case ResponseErrorType.Http:
                        throw new InvalidOperationException($"Discovery endpoint {options.Authority} is unavailable: {disco.Error}");
                    case ResponseErrorType.PolicyViolation:
                        throw new InvalidOperationException($"Policy error while contacting the discovery endpoint {options.Authority}: {disco.Error}");
                    case ResponseErrorType.Exception:
                        throw new InvalidOperationException($"Error parsing discovery document from {options.Authority}: {disco.Error}");
                }
            }

            return disco.IntrospectionEndpoint;
        }
=======
namespace IdentityModel.AspNetCore.OAuth2Introspection;

internal class PostConfigureOAuth2IntrospectionOptions : IPostConfigureOptions<OAuth2IntrospectionOptions>
{
    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    public PostConfigureOAuth2IntrospectionOptions(IHttpClientFactory httpClientFactory, IDistributedCache cache = null)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    public void PostConfigure(string name, OAuth2IntrospectionOptions options)
    {
        options.Validate();

        if (options.EnableCaching && _cache == null)
        {
            throw new ArgumentException("Caching is enabled, but no IDistributedCache is found in the services collection", nameof(_cache));
        }

        options.IntrospectionClient = new AsyncLazy<HttpClient>(() => InitializeIntrospectionClient(options));
    }

    private async Task<HttpClient> InitializeIntrospectionClient(OAuth2IntrospectionOptions options)
    {
        if (!options.IntrospectionEndpoint.IsPresent())
        {
            options.IntrospectionEndpoint = await GetIntrospectionEndpointFromDiscoveryDocument(options).ConfigureAwait(false);
        }

        return _httpClientFactory.CreateClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName);
    }

    private async Task<string> GetIntrospectionEndpointFromDiscoveryDocument(OAuth2IntrospectionOptions options)
    {
        var client = _httpClientFactory.CreateClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName);

        var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = options.Authority,
            Policy = options?.DiscoveryPolicy ?? new DiscoveryPolicy()
        }).ConfigureAwait(false);

        if (disco.IsError)
        {
            switch (disco.ErrorType)
            {
                case ResponseErrorType.Http:
                    throw new InvalidOperationException($"Discovery endpoint {options.Authority} is unavailable: {disco.Error}");
                case ResponseErrorType.PolicyViolation:
                    throw new InvalidOperationException($"Policy error while contacting the discovery endpoint {options.Authority}: {disco.Error}");
                case ResponseErrorType.Exception:
                    throw new InvalidOperationException($"Error parsing discovery document from {options.Authority}: {disco.Error}");
            }
        }

        return disco.IntrospectionEndpoint;
>>>>>>> After
IdentityModel.AspNetCore.OAuth2Introspection.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace IdentityModel.AspNetCore.OAuth2Introspection;

internal class PostConfigureOAuth2IntrospectionOptions : IPostConfigureOptions<OAuth2IntrospectionOptions>
{
    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    public PostConfigureOAuth2IntrospectionOptions(IHttpClientFactory httpClientFactory, IDistributedCache cache = null)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    public void PostConfigure(string name, OAuth2IntrospectionOptions options)
    {
        options.Validate();

        if (options.EnableCaching && _cache == null)
        {
            throw new ArgumentException("Caching is enabled, but no IDistributedCache is found in the services collection", nameof(_cache));
        }

        options.IntrospectionClient = new AsyncLazy<HttpClient>(() => InitializeIntrospectionClient(options));
    }

    private async Task<HttpClient> InitializeIntrospectionClient(OAuth2IntrospectionOptions options)
    {
        if (!options.IntrospectionEndpoint.IsPresent())
        {
            options.IntrospectionEndpoint = await GetIntrospectionEndpointFromDiscoveryDocument(options).ConfigureAwait(false);
        }

        return _httpClientFactory.CreateClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName);
    }

    private async Task<string> GetIntrospectionEndpointFromDiscoveryDocument(OAuth2IntrospectionOptions options)
    {
        var client = _httpClientFactory.CreateClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName);

        var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = options.Authority,
            Policy = options?.DiscoveryPolicy ?? new DiscoveryPolicy()
        }).ConfigureAwait(false);

        if (disco.IsError)
        {
            switch (disco.ErrorType)
            {
                case ResponseErrorType.Http:
                    throw new InvalidOperationException($"Discovery endpoint {options.Authority} is unavailable: {disco.Error}");
                case ResponseErrorType.PolicyViolation:
                    throw new InvalidOperationException($"Policy error while contacting the discovery endpoint {options.Authority}: {disco.Error}");
                case ResponseErrorType.Exception:
                    throw new InvalidOperationException($"Error parsing discovery document from {options.Authority}: {disco.Error}");
            }
        }

        return disco.IntrospectionEndpoint;
    }
}
