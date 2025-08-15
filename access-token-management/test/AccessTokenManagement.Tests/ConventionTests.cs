// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Reflection;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AccessTokenManagement.OTel;

namespace Duende.AccessTokenManagement;

public class ConventionTests(ITestOutputHelper output)
{
    public static readonly Assembly AtmAssembly = typeof(ClientCredentialsToken).Assembly;
    public static readonly Assembly AtmOidcAssembly = typeof(UserToken).Assembly;
    public static readonly Type[] AllTypes =
        AtmAssembly.GetTypes()
        .Union(AtmOidcAssembly.GetTypes())
        .ToArray();

    [Fact]
    public void All_strongly_typed_strings_Have_private_value()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            var stringFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(string)).ToList();
            stringFields.ShouldNotBeEmpty($"{type.Name} should have a private string field.");
            stringFields.All(f => f.IsPrivate).ShouldBeTrue($"{type.Name} should have its string value as private.");
        }
    }

    [Fact]
    public void All_strongly_typed_strings_are_readonly_struct()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            type.IsValueType.ShouldBeTrue($"{type.Name} should be a value type (struct).");
            type.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute));
        }
    }

    [Fact]
    public void All_strongly_typed_strings_have_internal_create_method()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            var buildMethod = type.GetMethods(BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Create");
            buildMethod.ShouldBeNull("The IStonglyTypedString defines a Create method, but it should be implemented explicitly on the interface, not on the type. \r\n IE: " +
                "    static AccessTokenString IStonglyTypedString<AccessTokenString>.Create(string result) => new(result);");
        }
    }

    [Fact]
    public void All_strongly_typed_strings_should_have_public_constructor_that_throws()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            // Find the public constructor that takes a single string parameter
            var ctor = type.GetConstructor([]);
            ctor.ShouldNotBeNull($"{type.Name} should have a public parameterless constructor.");

            // Try to invoke the constructor with a value and expect an exception
            var ex = Should.Throw<TargetInvocationException>(() => ctor.Invoke([]));
            ex.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldContain("Can't create null value");
        }
    }
    [Fact]
    public void All_strongly_typed_strings_should_have_only_expected_constructors()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            // Get all instance constructors (public and non-public)
            var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // There must be exactly two constructors
            ctors.Length.ShouldBe(2, $"{type.Name} should have exactly two constructors: one public parameterless and one private with a single string parameter.");

            // Find the public parameterless constructor
            var publicParameterlessCtor = ctors.FirstOrDefault(c =>
                c.IsPublic &&
                c.GetParameters().Length == 0);

            publicParameterlessCtor.ShouldNotBeNull($"{type.Name} should have a public parameterless constructor.");

            // Find the private constructor with a single string parameter
            var privateStringCtor = ctors.FirstOrDefault(c =>
                c.IsPrivate &&
                c.GetParameters().Length == 1 &&
                c.GetParameters()[0].ParameterType == typeof(string));

            privateStringCtor.ShouldNotBeNull($"{type.Name} should have a private constructor with a single string parameter.");
        }
    }

    [Fact]
    public void All_types_in_Internal_namespace_should_be_internal()
    {
        // Find all types in the 'Duende.AccessTokenManagement.Internal' namespace
        var internalTypes = AllTypes
            .Where(t => t.Namespace?.Contains(".Internal") ?? false)
            .ToList();

        internalTypes.ShouldNotBeEmpty("No types found in the 'Duende.AccessTokenManagement.Internal' namespace.");

        foreach (var type in internalTypes)
        {
            IsInternal(type).ShouldBeTrue($"{type.Name} should be internal.");
        }
    }

    [Fact]
    public void All_types_not_in_Internal_namespace_should_be_sealed_or_static()
    {
        Type[] exclusions = [typeof(TokenRequestParameters), typeof(OTelParameters)];

        // Find all types NOT in a '.Internal' namespace
        var nonInternalTypes = AllTypes
            .Where(t => t.Namespace != null && !t.Namespace.Contains(".Internal"))
            .Where(t => t.IsClass && !t.IsAbstract) // Only consider non-abstract classes
            .Where(t => !exclusions.Contains(t))
            .ToList();

        nonInternalTypes.ShouldNotBeEmpty("No non-internal types found.");

        foreach (var type in nonInternalTypes)
        {
            // A static class is abstract and sealed
            var isStatic = type.IsAbstract && type.IsSealed;
            var isSealed = type.IsSealed;

            (isSealed || isStatic).ShouldBeTrue(
                $"{type.FullName} should be sealed or static (abstract+sealed)."
            );
        }
    }

    [Fact]
    public void All_async_methods_should_end_with_Async_and_have_cancellation_token_as_last_parameter()
    {
        var failures = new List<string>();

        foreach (var type in AllTypes.Where(t => t.IsClass && !t.IsAbstract && !typeof(Delegate).IsAssignableFrom(t)))
        {
            // Get all public instance and static methods
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                .Where(m => typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType));

            foreach (var method in methods)
            {
                // 1. Name should end with 'Async'
                if (!method.Name.EndsWith("Async"))
                {
                    failures.Add($"{type.FullName}.{method.Name}: Async method should be suffixed with 'Async'.");
                }

                // 2. Last parameter should be a CancellationToken (if there are any parameters)
                var parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters.Last().ParameterType != typeof(System.Threading.CancellationToken))
                {
                    failures.Add($"{type.FullName}.{method.Name}: Async method should have a CancellationToken as the last parameter.");
                }
            }
        }

        foreach (var failure in failures)
        {
            output.WriteLine(failure);
        }

        failures.ShouldBeEmpty();
    }
    public static bool IsInternal(Type type)
    {
        if (type.IsNested)
        {
            return true;
        }
        return type.IsNestedPrivate || type.IsNotPublic;
    }



    private static List<Type> GetStrongTypedStringTypes()
    {
        // Find all types implementing IStringValue<TSelf>
        var stringValueTypes =
            AllTypes.Where(t => t.IsValueType && !t.IsAbstract)
            .SelectMany(t =>
                t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedValue<>)
                                                && i.GenericTypeArguments[0] == t)
                    .Select(_ => t))
            .Distinct()
            .ToList();
        return stringValueTypes;
    }
}
