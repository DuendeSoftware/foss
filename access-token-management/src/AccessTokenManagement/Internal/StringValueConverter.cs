// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Globalization;

namespace Duende.AccessTokenManagement.Internal;

internal class StringValueConverter<T> : TypeConverter where T : struct, IStronglyTypedString<T>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string stringValue)
        {
            return base.ConvertFrom(context, culture, value);
        }

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        // Use the TryParse method from IStringValue<T> to parse the string
        if (T.TryParse(stringValue, out var parsedValue, out var errors))
        {
            return parsedValue;
        }

        // If parsing fails, throw an exception with the errors
        throw new InvalidOperationException(
            $"Failed to convert '{stringValue}' to {typeof(T).Name}. Errors: {string.Join(", ", errors)}");
    }
}
