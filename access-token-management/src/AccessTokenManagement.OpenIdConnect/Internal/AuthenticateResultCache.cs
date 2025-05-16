// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authentication;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Per-request cache so that if SignInAsync is used, we won't re-read the old/cached AuthenticateResult from the handler.
/// This requires this service to be added as scoped to the DI system.
/// Be VERY CAREFUL to not accidentally capture this service for longer than the appropriate DI scope - e.g., in an HttpClient.
///
/// Note, the authentication cache uses Scheme.Empty as the key for null SignInScheme.
/// </summary>
internal class AuthenticateResultCache
{
    private readonly Dictionary<Scheme, AuthenticateResult> _dictionary = new();

    public bool TryGetValue(Scheme? key, [MaybeNullWhen(false)] out AuthenticateResult value) =>
        _dictionary.TryGetValue(key ?? Scheme.Empty, out value);

    public AuthenticateResult this[Scheme? key]
    {
        get => _dictionary[key ?? Scheme.Empty];
        set => _dictionary[key ?? Scheme.Empty] = value;
    }
}
