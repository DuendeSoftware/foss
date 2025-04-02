// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;

namespace Duende.IdentityModel;

public class AuthorizeResponseTests
{
    [Fact]
    public void Error_Response_with_QueryString()
    {
        const string url = "http://server/callback?error=foo";

        var response = new AuthorizeResponse(url);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("foo");
    }

    [Fact]
    public void Error_Response_with_HashFragment()
    {
        const string url = "http://server/callback#error=foo";

        var response = new AuthorizeResponse(url);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("foo");
    }

    [Fact]
    public void Error_Response_with_QueryString_and_HashFragment()
    {
        const string url = "http://server/callback?error=foo#_=_";

        var response = new AuthorizeResponse(url);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("foo");
    }

    [Fact]
    public void Code_Response_with_QueryString()
    {
        const string url = "http://server/callback?code=foo&sid=123";

        var response = new AuthorizeResponse(url);

        response.IsError.ShouldBeFalse();
        response.Code.ShouldBe("foo");

        response.Values["sid"].ShouldBe("123");
        response.TryGet("sid").ShouldBe("123");
    }

    [Fact]
    public void AccessToken_Response_with_QueryString()
    {
        const string url = "http://server/callback#access_token=foo&sid=123";

        var response = new AuthorizeResponse(url);

        response.IsError.ShouldBeFalse();
        response.AccessToken.ShouldBe("foo");

        response.Values["sid"].ShouldBe("123");
        response.TryGet("sid").ShouldBe("123");
    }

    [Fact]
    public void AccessToken_Response_with_QueryString_and_HashFragment()
    {
        const string url = "http://server/callback?access_token=foo&sid=123#_=_";

        var response = new AuthorizeResponse(url);

        response.IsError.ShouldBeFalse();
        response.AccessToken.ShouldBe("foo");

        response.Values["sid"].ShouldBe("123");
        response.TryGet("sid").ShouldBe("123");
    }

    [Fact]
    public void AccessToken_Response_with_QueryString_and_Empty_Entry()
    {
        const string url = "http://server/callback?access_token=foo&&sid=123&";

        var response = new AuthorizeResponse(url);

        response.IsError.ShouldBeFalse();
        response.AccessToken.ShouldBe("foo");

        response.Values["sid"].ShouldBe("123");
        response.TryGet("sid").ShouldBe("123");
    }

    [Fact]
    public void form_post_format_should_parse()
    {
        const string form = "id_token=foo&code=bar&scope=baz&session_state=quux";
        var response = new AuthorizeResponse(form);

        response.IsError.ShouldBeFalse();
        response.IdentityToken.ShouldBe("foo");
        response.Code.ShouldBe("bar");
        response.Scope.ShouldBe("baz");
        response.Values["session_state"].ShouldBe("quux");
    }
}
