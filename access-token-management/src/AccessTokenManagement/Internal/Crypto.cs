// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Duende.IdentityModel;

namespace Duende.AccessTokenManagement.Internal;

internal static class Crypto
{
    /// <summary>
    /// Simple hashing algorithm that should only be used to obfuscate ephemeral data in a deterministic way 
    /// in logs, not for storing passwords
    /// </summary>
    /// <param name="data">The data to hash</param>
    /// <returns>Hash of the incoming data. </returns>
    public static string HashData(string data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(data));

        var leftPart = new byte[16];
        Array.Copy(hash, leftPart, 16);

        return Base64Url.Encode(leftPart);
    }
}
