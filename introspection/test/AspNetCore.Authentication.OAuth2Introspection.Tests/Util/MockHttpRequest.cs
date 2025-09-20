// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

internal class MockHttpRequest : HttpRequest
{
    public override Stream Body { get; set; } = null!;
    public override long? ContentLength { get; set; }
    public override string? ContentType { get; set; }
    public override IRequestCookieCollection Cookies { get; set; } = null!;
    public override IFormCollection Form { get; set; } = null!;
    public override bool HasFormContentType { get; } = false;
    public override IHeaderDictionary Headers { get; } = new HeaderDictionary();
    public override HostString Host { get; set; }
    public override HttpContext HttpContext { get; } = null!;
    public override bool IsHttps { get; set; }
    public override string Method { get; set; } = null!;
    public override PathString Path { get; set; }
    public override PathString PathBase { get; set; }
    public override string Protocol { get; set; } = null!;
    public override IQueryCollection Query { get; set; } = null!;
    public override QueryString QueryString { get; set; }
    public override string Scheme { get; set; } = null!;

    public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
