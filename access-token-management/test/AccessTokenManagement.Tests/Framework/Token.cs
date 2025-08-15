// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Framework;

public record Token
{
    public string? access_token { get; init; }
    public string? token_type { get; init; }
    public string? scope { get; init; }
    public int? expires_in { get; init; }
}
