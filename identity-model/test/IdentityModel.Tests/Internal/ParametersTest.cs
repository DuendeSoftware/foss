// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;


namespace Duende.IdentityModel.Internal;

public class ParametersTest
{
    private const string Key = "custom";
    private const string Value = "custom";

    private readonly Parameters Parameters = new Parameters();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddOptional_with_missing_key_should_fail(string missingKey)
    {
        var act = () => Parameters.AddOptional(missingKey, Value);
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe("key");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddOptional_with_empty_value_should_not_be_added(string emptyValue)
    {
        Parameters.AddOptional(Key, emptyValue);
        Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void AddOptional_with_key_and_value_should_add()
    {
        Parameters.AddOptional(Key, Value);
        Parameters.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(Value)]
    [InlineData("different value")]
    public void AddOptional_with_duplicate_key_should_fail(string value)
    {
        Parameters.AddOptional(Key, Value);
        var act = () => Parameters.AddOptional(Key, value);
        var exception = act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe($"Duplicate parameter: {Key}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddOptional_with_duplicate_key_without_a_value_should_noop(string emptyValue)
    {
        Parameters.Add(Key, Value);
        Parameters.AddOptional(Key, emptyValue);
        Parameters.Count.ShouldBe(1);
    }

    [Fact]
    public void AddOptional_with_allow_duplicates_should_add_values()
    {
        Parameters.Add(Key, Value);
        Parameters.AddOptional(Key, "new value", allowDuplicates: true);
        Parameters.Count.ShouldBe(2);
        Parameters.GetValues(Key).Count().ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddOptional_with_allow_duplicates_should_not_add_empty_value(string emptyValue)
    {
        var parameters = new Parameters
        {
            { Key, Value}
        };

        parameters.AddOptional(Key, emptyValue, allowDuplicates: true);

        parameters.Count.ShouldBe(1);
        parameters.GetValues(Key).Count().ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddRequired_with_missing_key_should_fail(string missingKey)
    {
        var act = () => Parameters.AddRequired(missingKey, Value);
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe("key");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddRequired_with_empty_value_should_fail(string emptyValue)
    {
        var act = () => Parameters.AddRequired(Key, emptyValue);
        var exception = act.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe(Key);
    }

    [Fact]
    public void AddRequired_with_key_and_value_should_add()
    {
        Parameters.AddRequired(Key, Value);
        Parameters.Count.ShouldBe(1);
    }

    [Fact]
    public void AddRequired_with_empty_value_and_existing_parameter_should_noop()
    {
        var parameters = new Parameters();
        parameters.AddRequired(Key, Value);
        parameters.AddRequired(Key, null);
        parameters.AddRequired(Key, "");

        parameters.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddRequired_with_empty_value_and_allowEmptyValue_should_add(string emptyValue)
    {
        var parameters = new Parameters();

        parameters.AddRequired(Key, emptyValue, allowEmptyValue: true);
        parameters.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    // This test name is a mouthful! We want to make sure that we can add a
    // duplicate empty value by setting the allowDuplicates and
    // allowEmptyValue parameters.
    public void AddRequired_with_duplicate_empty_value_and_allowEmptyValue_and_allowDuplicates_should_add(string emptyValue)
    {
        var parameters = new Parameters
        {
            { Key, Value}
        };
        parameters.AddRequired(Key, emptyValue, allowDuplicates: true, allowEmptyValue: true);

        parameters.Count.ShouldBe(2);
        parameters[Key].Count().ShouldBe(2);
    }

    [Fact]
    public void AddRequired_with_duplicate_key_and_distinct_values_should_fail()
    {
        var parameters = new Parameters
        {
            { Key, Value}
        };

        var act = () => parameters.AddRequired(Key, "new value");
        var exception = act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe($"Duplicate parameter: {Key}");
    }

    [Fact]
    public void AddRequired_with_duplicate_key_and_value_should_noop()
    {
        Parameters.AddRequired(Key, Value);
        Parameters.AddRequired(Key, Value);

        Parameters.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddRequired_with_duplicate_key_without_a_value_should_noop(string emptyValue)
    {
        Parameters.Add(Key, Value);
        Parameters.AddRequired(Key, emptyValue);
        Parameters.Count.ShouldBe(1);
    }

    [Fact]
    public void Default_add_does_not_replace()
    {
        var key = "custom";
        var value = "custom";
        var parameters = new Parameters();

        parameters.Add(key, value);
        parameters.Add(key, value);

        parameters.Count.ShouldBe(2);
    }

    [Fact]
    public void Add_with_single_replace_works_as_expected()
    {
        var key = "custom";
        var value = "custom";
        var parameters = new Parameters();

        parameters.Add(key, value);
        parameters.Add(key, value, ParameterReplaceBehavior.Single);

        parameters.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_with_all_replace_works_as_expected()
    {
        var key = "custom";
        var value = "custom";
        var parameters = new Parameters();

        parameters.Add(key, value);
        parameters.Add(key, value, ParameterReplaceBehavior.All);

        parameters.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_with_single_replace_but_multiple_exist_should_throw()
    {
        var key = "custom";
        var parameters = new Parameters();

        parameters.Add(key, "value1");
        parameters.Add(key, "value2");

        var act = () => parameters.Add(key, "value3", ParameterReplaceBehavior.Single);
        act.ShouldThrow<InvalidOperationException>();
    }
}
