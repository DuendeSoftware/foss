// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Internal;

namespace Duende.IdentityModel.Client;

/// <summary>
/// Helper for caching discovery documents.
/// </summary>
public class DiscoveryCache : IDiscoveryCache
{
    private DateTime _nextReload = DateTime.MinValue;
    private AsyncLazy<DiscoveryDocumentResponse>? _lazyResponse;

    private readonly DiscoveryPolicy _policy;
    private readonly Func<HttpMessageInvoker> _getHttpClient;
    private readonly string? _authority;

    /// <summary>
    /// Initialize instance of DiscoveryCache with passed authority.
    /// </summary>
    /// <param name="authority">Base address or discovery document endpoint.</param>
    /// <param name="policy">The policy.</param>
    public DiscoveryCache(string authority, DiscoveryPolicy? policy = null)
    {
        _authority = authority;
        _policy = policy ?? new DiscoveryPolicy();
        _getHttpClient = () => new HttpClient();
    }

    /// <summary>
    /// Initialize instance of DiscoveryCache with passed authority.
    /// </summary>
    /// <param name="authority">Base address or discovery document endpoint.</param>
    /// <param name="httpClientFunc">The HTTP client function.</param>
    /// <param name="policy">The policy.</param>
    public DiscoveryCache(string authority, Func<HttpMessageInvoker> httpClientFunc, DiscoveryPolicy? policy = null)
    {
        _authority = authority;
        _policy = policy ?? new DiscoveryPolicy();
        _getHttpClient = httpClientFunc ?? throw new ArgumentNullException(nameof(httpClientFunc));
    }

    /// <summary>
    /// Initialize instance of DiscoveryCache without authority - the HttpClient used must have a BaseAddress configured.
    /// </summary>
    /// <param name="httpClientFunc">The HTTP client function which must have a BaseAddress configured.</param>
    /// <param name="policy">The policy.</param>
    public DiscoveryCache(Func<HttpMessageInvoker> httpClientFunc, DiscoveryPolicy? policy = null)
    {
        _getHttpClient = httpClientFunc ?? throw new ArgumentNullException(nameof(httpClientFunc));
        _policy = policy ?? new DiscoveryPolicy();
        _authority = null;
    }

    /// <summary>
    /// Frequency to refresh discovery document. Defaults to 24 hours.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Get the DiscoveryResponse either from cache or from discovery endpoint.
    /// </summary>
    /// <returns></returns>
    public Task<DiscoveryDocumentResponse> GetAsync()
    {
        if (_nextReload <= DateTime.UtcNow)
        {
            Refresh();
        }

        return _lazyResponse!.Value;
    }

    /// <summary>
    /// Marks the discovery document as stale and will trigger a request to the discovery endpoint on the next request to get the DiscoveryResponse.
    /// </summary>
    public void Refresh() => _lazyResponse = new AsyncLazy<DiscoveryDocumentResponse>(GetResponseAsync);

    private async Task<DiscoveryDocumentResponse> GetResponseAsync()
    {
        var client = _getHttpClient();
        var httpClient = client as HttpClient;
        var address = _authority ?? httpClient?.BaseAddress?.AbsoluteUri;
        if (address.IsMissing())
        {
            throw new InvalidOperationException("DiscoveryCache cannot determine the authority. Either pass the authority in the constructor or pass httpClientFunc which returns an instance of HttpClient with a BaseAddress.");
        }

        var result = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = address,
            Policy = _policy
        }).ConfigureAwait();

        if (result.IsError)
        {
            Refresh();
            _nextReload = DateTime.MinValue;
        }
        else
        {
            _nextReload = DateTime.UtcNow.Add(CacheDuration);
        }

        return result;
    }
}
