// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Duende.AccessTokenManagement.Tests;

public class TestBrowserClient : HttpClient
{
    class CookieHandler : DelegatingHandler
    {
        public CookieContainer CookieContainer { get; } = new();
        public Uri CurrentUri { get; private set; } = default!;
        public HttpResponseMessage LastResponse { get; private set; } = default!;

        public CookieHandler(HttpMessageHandler next)
            : base(next)
        {
        }

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

    private CookieHandler _handler;

    public CookieContainer CookieContainer => _handler.CookieContainer;
    public Uri CurrentUri => _handler.CurrentUri;
    public HttpResponseMessage LastResponse => _handler.LastResponse;

    public TestBrowserClient(HttpMessageHandler handler)
        : this(new CookieHandler(handler))
    {
    }

    private TestBrowserClient(CookieHandler handler)
        : base(handler) => _handler = handler;

    public Cookie? GetCookie(string name) => GetCookie(_handler.CurrentUri.ToString(), name);

    public Cookie? GetCookie(string uri, string name) => _handler.CookieContainer.GetCookies(new Uri(uri)).Cast<Cookie>().Where(x => x.Name == name).FirstOrDefault();

    public void RemoveCookie(string name) => RemoveCookie(CurrentUri.ToString(), name);

    public void RemoveCookie(string uri, string name)
    {
        var cookie = CookieContainer.GetCookies(new Uri(uri)).Cast<Cookie>().Where(x => x.Name == name).FirstOrDefault();
        if (cookie != null)
        {
            cookie.Expired = true;
        }
    }

    public async Task FollowRedirectAsync()
    {
        LastResponse.StatusCode.ShouldBe((HttpStatusCode)302);
        var location = LastResponse.Headers.Location!.ToString();
        await GetAsync(location);
    }

    public Task<HttpResponseMessage> PostFormAsync(HtmlForm form) => PostAsync(form.Action, new FormUrlEncodedContent(form.Inputs));

    public Task<HtmlForm> ReadFormAsync(string? selector = null) => ReadFormAsync(LastResponse, selector);

    public async Task<HtmlForm> ReadFormAsync(HttpResponseMessage response, string? selector = null)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var htmlForm = new HtmlForm();

        var html = await response.Content.ReadAsStringAsync();

        var parser = new HtmlParser();
        var dom = parser.ParseDocument(html);

        var form = dom.QuerySelector(selector ?? "form");
        form.ShouldNotBeNull();

        var postUrl = form.GetAttribute("action")!;
        if (!postUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (postUrl.StartsWith("/"))
            {
                postUrl = CurrentUri.Scheme + "://" + CurrentUri.Authority + postUrl;
            }
            else
            {
                postUrl = CurrentUri + postUrl;
            }
        }
        htmlForm.Action = postUrl;


        var data = new Dictionary<string, string?>();

        var inputs = form.QuerySelectorAll("input");
        foreach (var input in inputs)
        {
            var name = input.GetAttribute("name")!;
            var value = input.GetAttribute("value");

            data[name] = value;
        }
        htmlForm.Inputs = data;

        return htmlForm;
    }


    private Task<string> ReadElementTextAsync(string selector) => ReadElementTextAsync(LastResponse, selector);

    private async Task<string> ReadElementTextAsync(HttpResponseMessage response, string selector)
    {
        var html = await response.Content.ReadAsStringAsync();
        var parser = new HtmlParser();
        var dom = parser.ParseDocument(html);
        var element = dom.QuerySelector(selector)!;
        return element.Text();
    }

    private Task<string> ReadElementAttributeAsync(string selector, string attribute) => ReadElementAttributeAsync(LastResponse, selector, attribute);

    private async Task<string> ReadElementAttributeAsync(HttpResponseMessage response, string selector, string attribute)
    {
        var html = await response.Content.ReadAsStringAsync();
        var parser = new HtmlParser();
        var dom = parser.ParseDocument(html);
        var element = dom.QuerySelector(selector)!;
        return element.GetAttribute(attribute)!;
    }

    private Task AssertExistsAsync(string selector) => AssertExistsAsync(LastResponse, selector);

    private async Task AssertExistsAsync(HttpResponseMessage response, string selector)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        var parser = new HtmlParser();
        var dom = parser.ParseDocument(html);
        var element = dom.QuerySelector(selector);
        element.ShouldNotBeNull();
    }

    public Task AssertNotExistsAsync(string selector) => AssertNotExistsAsync(selector);

    public async Task AssertNotExistsAsync(HttpResponseMessage response, string selector)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        var parser = new HtmlParser();
        var dom = parser.ParseDocument(html);
        var element = dom.QuerySelector(selector);
        element.ShouldNotBeNull();
    }

    public Task AssertErrorPageAsync(string? error = null) => AssertErrorPageAsync(LastResponse, error);
    public async Task AssertErrorPageAsync(HttpResponseMessage response, string? error = null)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await AssertExistsAsync(response, ".error-page");

        if (!string.IsNullOrWhiteSpace(error))
        {
            var errorText = await ReadElementTextAsync(response, ".alert.alert-danger");
            errorText.ShouldContain(error);
        }
    }

    public Task AssertValidationErrorAsync(string? error = null) => AssertValidationErrorAsync(error);
    public async Task AssertValidationErrorAsync(HttpResponseMessage response, string? error = null)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await AssertExistsAsync(response, ".validation-summary-errors");

        if (!string.IsNullOrWhiteSpace(error))
        {
            var errorText = await ReadElementTextAsync(response, ".validation-summary-errors");
            errorText.ToLowerInvariant().ShouldContain(error.ToLowerInvariant());
        }
    }
}

[DebuggerDisplay("{Action}, Inputs: {Inputs.Count}")]
public class HtmlForm
{
    public HtmlForm(string? action = null) => Action = action;

    public string? Action { get; set; }
    public Dictionary<string, string?> Inputs { get; set; } = new Dictionary<string, string?>();

    public string? this[string key]
    {
        get
        {
            if (Inputs.ContainsKey(key))
            {
                return Inputs[key];
            }

            return null;
        }
        set => Inputs[key] = value;
    }
}
