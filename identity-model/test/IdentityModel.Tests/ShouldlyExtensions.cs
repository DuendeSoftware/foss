// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.IdentityModel;

internal static class ShouldlyExtensions
{
    public static void ShouldBe<T>(this IEnumerable<T> actual, IEnumerable<T> expected, IComparer<T> comparer, string customMessage = null)
    {
        if (actual is null && expected is null)
        {
            return; // Both are null, so they're equal.
        }

        actual.ShouldNotBeNull(customMessage);
        expected.ShouldNotBeNull(customMessage);

        var actualList = new List<T>(actual);
        var expectedList = new List<T>(expected);

        actualList.Count.ShouldBe(expectedList.Count, customMessage);

        for (var i = 0; i < actualList.Count; i++)
        {
            var comparisonResult = comparer.Compare(actualList[i], expectedList[i]);
            if (comparisonResult != 0)
            {
                throw new ShouldAssertException($@"
Collections differ at index {i}.
Actual: {actualList[i]}
Expected: {expectedList[i]}
{customMessage}");
            }
        }
    }
}
