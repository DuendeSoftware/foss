// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public readonly record struct ForceTokenRenewal(bool Value)
{
    public static implicit operator ForceTokenRenewal(bool value) => new(value);
}
