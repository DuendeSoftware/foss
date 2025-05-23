// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Headers;
using System.Text;

namespace Duende.IdentityModel.Client;

/// <summary>
/// HTTP Basic Authentication authorization header for RFC6749 client authentication
/// </summary>
/// <seealso cref="System.Net.Http.Headers.AuthenticationHeaderValue" />
public class BasicAuthenticationOAuthHeaderValue : AuthenticationHeaderValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthenticationOAuthHeaderValue"/> class.
    /// </summary>
    /// <param name="userName">Name of the user.</param>
    /// <param name="password">The password.</param>
    public BasicAuthenticationOAuthHeaderValue(string userName, string password)
        : base("Basic", EncodeCredential(userName, password))
    { }

    /// <summary>
    /// Encodes the credential.
    /// </summary>
    /// <param name="userName">Name of the user.</param>
    /// <param name="password">The password.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">userName</exception>
    public static string EncodeCredential(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentNullException(nameof(userName));
        }

        if (password == null)
        {
            password = "";
        }

        var encoding = Encoding.UTF8;
        var credential = $"{UrlEncode(userName)}:{UrlEncode(password)}";

        return Convert.ToBase64String(encoding.GetBytes(credential));
    }

    private static string UrlEncode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return Uri.EscapeDataString(value).Replace("%20", "+");
    }
}
