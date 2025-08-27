// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Internal;

namespace Duende.AccessTokenManagement.Framework;
internal class FakeTimeProvider(Func<DateTimeOffset> utcNow) : TimeProvider, ISystemClock
{
    public override DateTimeOffset GetUtcNow() => utcNow();
    public DateTimeOffset UtcNow => utcNow();
}
