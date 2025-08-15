// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.AccessTokenManagement.AccessTokenHandler.Helpers;

internal class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
