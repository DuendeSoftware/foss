// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;

namespace Duende.AccessTokenManagement.Framework;

public class TestBrowserClient : HttpClient
{
    public TestBrowserClient(HttpMessageHandler handler)
        : this(new CookieHandler(handler))
    {
    }

    private TestBrowserClient(CookieHandler handler)
        : base(handler)
    {
    }

    private class CookieHandler(HttpMessageHandler next) : DelegatingHandler(next)
    {
        private CookieContainer CookieContainer { get; } = new();

        public Uri CurrentUri { get; private set; } = null!;

        public HttpResponseMessage LastResponse { get; private set; } = null!;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CurrentUri = request.RequestUri!;
            var cookieHeader = CookieContainer.GetCookieHeader(request.RequestUri!);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Add("Cookie", cookieHeader);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.Headers.Contains("Set-Cookie"))
            {
                var responseCookieHeader = string.Join(",", response.Headers.GetValues("Set-Cookie"));
                CookieContainer.SetCookies(request.RequestUri!, responseCookieHeader);
            }

            LastResponse = response;

            return response;
        }
    }
}
