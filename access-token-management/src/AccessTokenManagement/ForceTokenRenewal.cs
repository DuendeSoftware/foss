// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Indicates whether token retrieval should bypass normal cache reuse and force renewal.
/// </summary>
/// <param name="Value"><see langword="true"/> to force token renewal; otherwise <see langword="false"/>.</param>
public readonly record struct ForceTokenRenewal(bool Value);
