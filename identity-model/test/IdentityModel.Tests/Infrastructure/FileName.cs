// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

namespace Duende.IdentityModel.Infrastructure;

internal static class FileName
{
    public static string Create(string name) => Path.Combine(UnitTestsPath(), "documents", name);

    private static string UnitTestsPath([CallerFilePath] string path = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), ".."));
}
