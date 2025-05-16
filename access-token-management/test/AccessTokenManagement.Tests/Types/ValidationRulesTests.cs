// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

public class ValidationRulesTests
{
    [Fact]
    public void MaxLength_ValidString_ReturnsTrue()
    {
        // Arrange
        var maxLength = 10;
        var rule = ValidationRules.MaxLength(maxLength);
        var validString = "short";

        // Act
        var isValid = rule(validString, out var message);

        // Assert
        isValid.ShouldBeTrue();
        message.ShouldBeEmpty();
    }

    [Fact]
    public void MaxLength_InvalidString_ReturnsFalse()
    {
        // Arrange
        var maxLength = 10;
        var rule = ValidationRules.MaxLength(maxLength);
        var invalidString = new string('a', maxLength + 1);

        // Act
        var isValid = rule(invalidString, out var message);

        // Assert
        isValid.ShouldBeFalse();
        message.ShouldContain($"The string exceeds maximum length {maxLength}.");
    }

    [Fact]
    public void AlphaNumeric_ValidString_ReturnsTrue()
    {
        // Arrange
        var rule = ValidationRules.AlphaNumeric();
        var validString = "Alpha123";

        // Act
        var isValid = rule(validString, out var message);

        // Assert
        isValid.ShouldBeTrue();
        message.ShouldBeEmpty();
    }

    [Fact]
    public void AlphaNumeric_InvalidString_ReturnsFalse()
    {
        // Arrange
        var rule = ValidationRules.AlphaNumeric();
        var invalidString = "Alpha@123";

        // Act
        var isValid = rule(invalidString, out var message);

        // Assert
        isValid.ShouldBeFalse();
        message.ShouldContain("The string must be alphanumeric.");
    }

    [Fact]
    public void Regex_ValidString_ReturnsTrue()
    {
        // Arrange
        var regex = new Regex(@"^\d+$");
        var rule = ValidationRules.Regex(regex, "The string must contain only digits.");
        var validString = "12345";

        // Act
        var isValid = rule(validString, out var message);

        // Assert
        isValid.ShouldBeTrue();
        message.ShouldBeEmpty();
    }

    [Fact]
    public void Regex_InvalidString_ReturnsFalse()
    {
        // Arrange
        var regex = new Regex(@"^\d+$");
        var rule = ValidationRules.Regex(regex, "The string must contain only digits.");
        var invalidString = "123a";

        // Act
        var isValid = rule(invalidString, out var message);

        // Assert
        isValid.ShouldBeFalse();
        message.ShouldContain("The string must contain only digits.");
    }

    [Fact]
    public void Uri_ValidUri_ReturnsTrue()
    {
        // Arrange
        var rule = ValidationRules.Uri();
        var validUri = "https://example.com";

        // Act
        var isValid = rule(validUri, out var message);

        // Assert
        isValid.ShouldBeTrue();
        message.ShouldBeEmpty();
    }

    [Fact]
    public void Uri_InvalidUri_ReturnsFalse()
    {
        // Arrange
        var rule = ValidationRules.Uri();
        var invalidUri = "not-a-valid-uri";

        // Act
        var isValid = rule(invalidUri, out var message);

        // Assert
        isValid.ShouldBeFalse();
        message.ShouldContain("The string must be a valid Uri.");
    }

    [Fact]
    public void Authority_ValidAuthority_ReturnsTrue()
    {
        // Arrange
        var rule = ValidationRules.Authority();
        var validAuthority = "https://example.com/";

        // Act
        var isValid = rule(validAuthority, out var message);

        // Assert
        isValid.ShouldBeTrue();
        message.ShouldBeEmpty();
    }

    [Fact]
    public void Authority_InvalidAuthority_ReturnsFalse()
    {
        // Arrange
        var rule = ValidationRules.Authority();
        var invalidAuthority = "https://example.com/path";

        // Act
        var isValid = rule(invalidAuthority, out var message);

        // Assert
        isValid.ShouldBeFalse();
        message.ShouldContain("The string must be a valid Authority.");
    }
}
