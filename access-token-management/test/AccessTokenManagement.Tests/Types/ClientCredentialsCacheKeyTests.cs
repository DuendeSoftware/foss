// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Types;

public class ClientCredentialsCacheKeyTests
{
    [Fact]
    public void Parse_ValidValue_ReturnsCacheKey()
    {
        // Arrange
        var validValue = "valid_key";

        // Act
        var result = ClientCredentialsCacheKey.Parse(validValue);

        // Assert
        result.ToString().ShouldBe(validValue);
    }

    [Fact]
    public void Parse_InvalidValue_ThrowsException()
    {
        // Arrange
        var invalidValue = new string('a', ClientCredentialsCacheKey.MaxLength + 1); // Exceeds max length

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => ClientCredentialsCacheKey.Parse(invalidValue));
        exception.Message.ShouldContain("exceeds maximum length");
    }

    [Fact]
    public void TryParse_ValidValue_ReturnsTrueAndCacheKey()
    {
        // Arrange
        var validValue = "valid_key";

        // Act
        var success = ClientCredentialsCacheKey.TryParse(validValue, out var result, out var errors);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.ToString().ShouldBe(validValue);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void TryParse_InvalidValue_ReturnsFalseAndErrors()
    {
        // Arrange
        var invalidValue = new string('a', ClientCredentialsCacheKey.MaxLength + 1); // Exceeds max length

        // Act
        var success = ClientCredentialsCacheKey.TryParse(invalidValue, out var result, out var errors);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
        errors.ShouldNotBeEmpty();
        errors[0].ShouldContain("exceeds maximum length");
    }
}
