// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;

[JsonSerializable(typeof(IEnumerable<ClaimLite>))]
internal partial class DuendeIntrospectionSerializationContext : JsonSerializerContext;
