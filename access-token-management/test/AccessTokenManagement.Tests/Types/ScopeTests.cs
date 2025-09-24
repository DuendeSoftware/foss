// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Types;

public class ScopeTests
{
    [Theory]
    [InlineData("!")]
    [InlineData("#")]
    [InlineData("$")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("^")]
    [InlineData("~")]
    [InlineData("[word]")]
    [InlineData("path/to/resource")]
    [InlineData("<tag>")]
    [InlineData("{foo:bar}")]
    [InlineData(":foo.v1.baz.{{bar}}.*")]
    public void Scope_with_valid_value_should_not_throw(string scopeValue) => Should.NotThrow(() => Scope.Parse(scopeValue));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    //Control Characters
    [InlineData("\t")]
    [InlineData("\n")]
    //Backslash and double quote
    [InlineData("\"")]
    [InlineData("\\")]
    [InlineData(" leadingspace")]
    [InlineData("trailingspace ")]
    public void Scope_with_invalid_value_should_throw(string scopeValue) => Should.Throw<InvalidOperationException>(() => Scope.Parse(scopeValue));
}
