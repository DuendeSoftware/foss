// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Types;

public class AccessTokenTypeTests
{
    [Fact]
    public void Can_change_to_scheme()
    {
        var type = AccessTokenType.Parse("dpop");
        var sceme1 = Scheme.Parse("dpop");

        var scheme = type.ToScheme();
    }
}
