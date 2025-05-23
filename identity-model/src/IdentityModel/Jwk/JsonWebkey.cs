// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable 1591
#nullable disable

namespace Duende.IdentityModel.Jwk;

/// <summary>
/// Represents a Json Web Key as defined in http://tools.ietf.org/html/rfc7517.
/// </summary>
public class JsonWebKey
{
    // kept private to hide that a List is used.
    // public member returns an IList.
    private IList<string> _certificateClauses = new List<string>();
    private IList<string> _keyops = new List<string>();

    /// <summary>
    /// Initializes an new instance of <see cref="JsonWebKey"/>.
    /// </summary>
    public JsonWebKey()
    {
    }

    /// <summary>
    /// Initializes an new instance of <see cref="JsonWebKey"/> from a json string.
    /// </summary>
    /// <param name="json">a string that contains JSON Web Key parameters in JSON format.</param>
    public JsonWebKey(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentNullException(nameof(json));
        }

        var key = JsonSerializer.Deserialize<JsonWebKey>(json, JwkSourceGenerationContext.Default.JsonWebKey);
        if (key == null)
        {
            throw new InvalidOperationException("malformed key");
        }

        Copy(key);
    }

    private void Copy(JsonWebKey key)
    {
        Alg = key.Alg;
        Crv = key.Crv;
        D = key.D;
        DP = key.DP;
        DQ = key.DQ;
        E = key.E;
        K = key.K;
        Kid = key.Kid;
        Kty = key.Kty;
        N = key.N;
        Oth = key.Oth;
        P = key.P;
        Q = key.Q;
        QI = key.QI;
        Use = key.Use;
        X5t = key.X5t;
        X5tS256 = key.X5tS256;
        X5u = key.X5u;
        X = key.X;
        Y = key.Y;

        _certificateClauses = new List<string>(key.X5c);
        _keyops = new List<string>(key.KeyOps);
    }

    /// <summary>
    /// Gets or sets the 'alg' (KeyType)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.Alg)]
    public string Alg { get; set; }

    /// <summary>
    /// Gets or sets the 'crv' (ECC - Curve)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.Crv)]
    public string Crv { get; set; }

    /// <summary>
    /// Gets or sets the 'd' (ECC - Private Key OR RSA - Private Exponent)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlUInt</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.D)]
    public string D { get; set; }

    /// <summary>
    /// Gets or sets the 'dp' (RSA - First Factor CRT Exponent)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlUInt</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.DP)]
    public string DP { get; set; }

    /// <summary>
    /// Gets or sets the 'dq' (RSA - Second Factor CRT Exponent)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlUInt</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.DQ)]
    public string DQ { get; set; }

    /// <summary>
    /// Gets or sets the 'e' (RSA - Exponent)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.E)]
    public string E { get; set; }

    /// <summary>
    /// Gets or sets the 'k' (Symmetric - Key Value)..
    /// </summary>
    /// Base64urlEncoding
    [JsonPropertyName(JsonWebKeyParameterNames.K)]
    public string K { get; set; }

    /// <summary>
    /// Gets or sets the 'key_ops' (Key Operations)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.KeyOps)]
    public IList<string> KeyOps
    {
        get => _keyops;
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException("KeyOps");
            }

            foreach (var keyOp in value)
            {
                _keyops.Add(keyOp);
            }
        }
    }

    /// <summary>
    /// Gets or sets the 'kid' (Key ID)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.Kid)]
    public string Kid { get; set; }

    /// <summary>
    /// Gets or sets the 'kty' (Key Type)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.Kty)]
    public string Kty { get; set; }

    /// <summary>
    /// Gets or sets the 'n' (RSA - Modulus)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlEncoding</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.N)]
    public string N { get; set; }

    /// <summary>
    /// Gets or sets the 'oth' (RSA - Other Primes Info)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.Oth)]
    public IList<string> Oth { get; set; }

    /// <summary>
    /// Gets or sets the 'p' (RSA - First Prime Factor)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlUInt</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.P)]
    public string P { get; set; }

    /// <summary>
    /// Gets or sets the 'q' (RSA - Second  Prime Factor)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlUInt</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.Q)]
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the 'qi' (RSA - First CRT Coefficient)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlUInt</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.QI)]
    public string QI { get; set; }

    /// <summary>
    /// Gets or sets the 'use' (Public Key Use)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.Use)]
    public string Use { get; set; }

    /// <summary>
    /// Gets or sets the 'x' (ECC - X Coordinate)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlEncoding</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.X)]
    public string X { get; set; }

    /// <summary>
    /// Gets the 'x5c' collection (X.509 Certificate Chain)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.X5c)]
    public IList<string> X5c
    {
        get => _certificateClauses;
        set
        {
            //if (value == null)
            //    throw LogHelper.LogException<ArgumentNullException>(LogMessages.IDX10001, "X5c");

            foreach (var clause in value)
            {
                _certificateClauses.Add(clause);
            }
        }
    }

    /// <summary>
    /// Gets or sets the 'x5t' (X.509 Certificate SHA-1 thumbprint)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.X5t)]
    public string X5t { get; set; }

    /// <summary>
    /// Gets or sets the 'x5t#S256' (X.509 Certificate SHA-1 thumbprint)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.X5tS256)]
    public string X5tS256 { get; set; }

    /// <summary>
    /// Gets or sets the 'x5u' (X.509 URL)..
    /// </summary>
    [JsonPropertyName(JsonWebKeyParameterNames.X5u)]
    public string X5u { get; set; }

    /// <summary>
    /// Gets or sets the 'y' (ECC - Y Coordinate)..
    /// </summary>
    /// <remarks> value is formated as: Base64urlEncoding</remarks>
    [JsonPropertyName(JsonWebKeyParameterNames.Y)]
    public string Y { get; set; }

    public int KeySize
    {
        get
        {
            if (Kty == JsonWebAlgorithmsKeyTypes.RSA)
            {
                return Base64Url.Decode(N).Length * 8;
            }
            else if (Kty == JsonWebAlgorithmsKeyTypes.EllipticCurve)
            {
                return Base64Url.Decode(X).Length * 8;
            }
            else if (Kty == JsonWebAlgorithmsKeyTypes.Octet)
            {
                return Base64Url.Decode(K).Length * 8;
            }
            else
            {
                return 0;
            }
        }
    }

    public bool HasPrivateKey
    {
        get
        {
            if (Kty == JsonWebAlgorithmsKeyTypes.RSA)
            {
                return D != null && DP != null && DQ != null && P != null && Q != null && QI != null;
            }
            else if (Kty == JsonWebAlgorithmsKeyTypes.EllipticCurve)
            {
                return D != null;
            }
            else
            {
                return false;
            }
        }
    }
}
