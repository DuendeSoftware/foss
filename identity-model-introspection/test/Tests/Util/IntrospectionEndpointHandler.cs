// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text;

namespace Tests.Util;

public class IntrospectionEndpointHandler : DelegatingHandler
{
    private readonly Behavior _behavior;

    public enum Behavior
    {
        Active,
        Inactive,
        Unauthorized
    }

    public bool SentIntrospectionRequest { get; set; } = false;

    public Dictionary<string, object> AdditionalValues { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, string> LastRequest { get; set; } = new Dictionary<string, string>();
    public string IntrospectionEndpoint { get; set; }
    public string DiscoveryEndpoint { get; set; }
    public bool IsDiscoveryFailureTest { get; set; } = false;

    public IntrospectionEndpointHandler(Behavior behavior, TimeSpan? ttl = null)
    {
        _behavior = behavior;

        if (ttl.HasValue)
        {
            AdditionalValues.Add("exp", DateTimeOffset.UtcNow.Add(ttl.Value).ToUnixTimeSeconds());
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri.AbsoluteUri.Contains("well-known"))
        {
            return SendDiscoveryAsync(request, cancellationToken);
        }
        else
        {
            return SendIntrospectionAsync(request, cancellationToken);
        }
    }

    protected Task<HttpResponseMessage> SendDiscoveryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsDiscoveryFailureTest)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        if (request.RequestUri.AbsoluteUri.ToString() == "https://authority.com/.well-known/openid-configuration")
        {
            DiscoveryEndpoint = request.RequestUri.AbsoluteUri;

            var data = new Dictionary<string, object>
            {
                { "issuer", "https://authority.com" },
                { "introspection_endpoint", "https://authority.com/introspection_endpoint" }
            };

            var json = SimpleJson.SimpleJson.SerializeObject(data);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    protected Task<HttpResponseMessage> SendIntrospectionAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentIntrospectionRequest = true;
        IntrospectionEndpoint = request.RequestUri.AbsoluteUri;

        LastRequest = ExtractFormContent(request);

        switch (_behavior)
        {
            case Behavior.Unauthorized:
                {
                    var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    return Task.FromResult(response);
                }
            case Behavior.Active:
                {
                    var responseObject = new Dictionary<string, object>
                {
                    {"active", true}
                };

                    foreach (var item in AdditionalValues)
                    {
                        responseObject.Add(item.Key, item.Value);
                    }

                    var json = SimpleJson.SimpleJson.SerializeObject(responseObject);

                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };

                    return Task.FromResult(response);
                }
            case Behavior.Inactive:
                {
                    var responseObject = new Dictionary<string, object>
                {
                    {"active", false}
                };

                    var json = SimpleJson.SimpleJson.SerializeObject(responseObject);

                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };

                    return Task.FromResult(response);
                }
            default:
                throw new NotImplementedException();
        }
    }

    private static Dictionary<string, string> ExtractFormContent(HttpRequestMessage request) => request.Content.ReadAsStringAsync().Result.Split("&")
            .Select(item => item.Split("=")).ToDictionary(item => item[0], item => Uri.UnescapeDataString(item[1]));
}
