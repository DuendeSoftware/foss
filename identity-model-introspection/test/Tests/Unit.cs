// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Tests.Util;

namespace Tests;

public static class Unit
{
    [Theory]
    [InlineData(null, new string[] { })]
    [InlineData(null, new string[] { "Basic XYZ" })]
    [InlineData(null, new string[] { "Basic XYZ", "Bearer ABC" })]
    [InlineData("ABC", new string[] { "Bearer ABC" })]
    [InlineData("ABC", new string[] { "Bearer  ABC " })]
    [InlineData("ABC", new string[] { "Bearer ABC", "Basic XYZ" })]
    [InlineData("ABC", new string[] { "Bearer ABC", "Bearer DEF" })]
    [InlineData("ABC", new string[] { "Bearer    ABC", "Bearer DEF" })]
    [InlineData("ABC", new string[] { "Bearer ABC   ", "Bearer DEF" })]
    public static void Token_From_Header(string expected, string[] headerValues)
    {
        var request = new MockHttpRequest();
        request.Headers.Append("Authorization", new Microsoft.Extensions.Primitives.StringValues(headerValues));

        var actual = TokenRetrieval.FromAuthorizationHeader()(request);
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, "?a=1")]
    [InlineData("", "?access_token=")]
    [InlineData("", "?access_token&access_token")]
    [InlineData("xyz", "?access_token=xyz")]
    [InlineData("xyz", "?a=1&access_token=xyz")]
    [InlineData("abc", "?access_token=abc&access_token=xyz")]
    public static void Token_From_Query(string expected, string queryString)
    {
        var request = new MockHttpRequest
        {
            Query = new QueryCollection(QueryHelpers.ParseQuery(queryString))
        };

        var actual = TokenRetrieval.FromQueryString()(request);
        actual.ShouldBe(expected);
    }
}
