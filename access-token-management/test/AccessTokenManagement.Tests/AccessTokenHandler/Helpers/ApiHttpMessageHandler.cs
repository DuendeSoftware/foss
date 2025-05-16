// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using RichardSzalay.MockHttp;

namespace Duende.AccessTokenManagement.AccessTokenHandlers.Helpers;

public class ApiHttpMessageHandler : MockHttpMessageHandler
{
    public Uri Uri = new Uri("https://api");

    public string LastUsedAccessToken = "";

    public void ExpectCallWithoutNonce(string replyWithNonce) => this.Expect(Uri.ToString())
            .Respond((request) =>
            {
                request.EnsureRequestUsesScheme("Bearer");
                request.GetNonce().ShouldBeNull();
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    .WithDpopError(newNonce: replyWithNonce);
            });

    public void ExpectCallWithNonce(string expectedNonce, string replyWithNonce) => this.Expect(Uri.ToString())
            .Respond((request) =>
            {
                request.EnsureRequestUsesScheme("Bearer");
                request.GetNonce().ShouldBe(expectedNonce);
                return new HttpResponseMessage(HttpStatusCode.OK)
                    .WithNonce(replyWithNonce);
            });

    public void ExpectCallWithScheme(string expectedScheme) => this.Expect(Uri.ToString())
            .Respond((request) =>
            {
                request.EnsureRequestUsesScheme(expectedScheme);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });


    public void DefaultRespondOkWithToken() =>
        this.When(Uri.ToString()).Respond((request) =>
        {
            request.EnsureRequestUsesScheme("Bearer");

            var response = new HttpResponseMessage(HttpStatusCode.OK);

            LastUsedAccessToken = request.Headers.Authorization?.Parameter ?? "";

            return Task.FromResult(response);
        });

    public void RespondOnceWithUnauthorized() =>
        this.Expect(Uri.ToString()).Respond((request) =>
        {
            request.EnsureRequestUsesScheme("Bearer");
            LastUsedAccessToken = request.Headers.Authorization?.Parameter ?? "";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });
}
