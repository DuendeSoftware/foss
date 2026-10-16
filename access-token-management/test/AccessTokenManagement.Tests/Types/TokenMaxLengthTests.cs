// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Types;

public class TokenMaxLengthTests
{
    [Fact]
    public void AccessToken_Parse_with_max_length_accepts_values_up_to_the_supplied_limit()
    {
        const int limit = AccessToken.MaxLength * 2;
        var atLimit = new string('a', limit);

        AccessToken.Parse(atLimit, limit).ToString().ShouldBe(atLimit);
        Should.Throw<InvalidOperationException>(() => AccessToken.Parse(new string('a', limit + 1), limit));
    }

    [Fact]
    public void RefreshToken_Parse_with_max_length_accepts_values_up_to_the_supplied_limit()
    {
        const int limit = RefreshToken.MaxLength * 2;
        var atLimit = new string('a', limit);

        RefreshToken.Parse(atLimit, limit).ToString().ShouldBe(atLimit);
        Should.Throw<InvalidOperationException>(() => RefreshToken.Parse(new string('a', limit + 1), limit));
    }

    [Fact]
    public void IdentityToken_Parse_with_max_length_accepts_values_up_to_the_supplied_limit()
    {
        const int limit = IdentityToken.MaxLength * 2;
        var atLimit = new string('a', limit);

        IdentityToken.Parse(atLimit, limit).ToString().ShouldBe(atLimit);
        Should.Throw<InvalidOperationException>(() => IdentityToken.Parse(new string('a', limit + 1), limit));
    }

    [Fact]
    public void AccessToken_Parse_without_max_length_still_enforces_default_MaxLength() =>
        Should.Throw<InvalidOperationException>(() => AccessToken.Parse(new string('a', AccessToken.MaxLength + 1)));

    [Fact]
    public void RefreshToken_Parse_without_max_length_still_enforces_default_MaxLength() =>
        Should.Throw<InvalidOperationException>(() => RefreshToken.Parse(new string('a', RefreshToken.MaxLength + 1)));

    [Fact]
    public void IdentityToken_Parse_without_max_length_still_enforces_default_MaxLength() =>
        Should.Throw<InvalidOperationException>(() => IdentityToken.Parse(new string('a', IdentityToken.MaxLength + 1)));

    [Fact]
    public void Parse_with_non_positive_max_length_throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AccessToken.Parse("token", 0));

    [Fact]
    public void IdentityToken_ParseOrDefault_with_non_positive_max_length_throws_even_when_value_is_null() =>
        Should.Throw<ArgumentOutOfRangeException>(() => IdentityToken.ParseOrDefault(null, 0));
}
