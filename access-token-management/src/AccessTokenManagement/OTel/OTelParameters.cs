// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.OTel;

/// <summary>
/// Log parameters as constants for consistency
/// Note, these will be inlined by the compiler to be used in the attributes. 
/// </summary>
internal class OTelParameters
{
    public const string Scheme = "Scheme";
    public const string Error = "Error";
    public const string ErrorDescription = "ErrorDescription";
    public const string Url = "Url";
    public const string ClientId = "ClientId";
    public const string RequestUrl = "RequestUrl";
    public const string ClientName = "ClientName";
    public const string Expiration = "Expiration";
    public const string TokenHash = "TokenHash";
    public const string User = "User";
    public const string Resource = "Resource";
    public const string Method = "Method";
    public const string CacheKey = "CacheKey";
    public const string TokenType = "TokenType";
    public const string ForceRenewal = "ForceRenewal";
    public const string StatusCode = "StatusCode";
    public const string Value = "Value";
}
