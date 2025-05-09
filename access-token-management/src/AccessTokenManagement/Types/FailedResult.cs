// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Types;

public record FailedResult(string Error, string? ErrorDescription = null) : TokenResult
{
    public override bool Succeeded { get; protected set; } = false;

    public override string ToString()
    {
        var description = string.IsNullOrEmpty(ErrorDescription) ? string.Empty : $" with description {ErrorDescription}";
        return $"Failed to retrieve access token due to {Error}{description}.";
    }
}
