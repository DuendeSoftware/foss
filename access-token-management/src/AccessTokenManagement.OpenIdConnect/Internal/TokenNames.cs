// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

record TokenNames(
    string Token,
    string TokenType,
    string DPoPKey,
    string Expires,
    string RefreshToken,
    string IdentityToken,
    string ClientId);
