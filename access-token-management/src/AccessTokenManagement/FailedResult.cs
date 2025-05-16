// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public sealed record FailedResult(string Error, string? ErrorDescription = null) : TokenResult
{
    public override string ToString()
    {
        var description = string.IsNullOrEmpty(ErrorDescription) ? string.Empty : $" with description {ErrorDescription}";
        return $"Failed to retrieve access token due to {Error}{description}.";
    }
}
