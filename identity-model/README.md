## About Duende.IdentityModel

Duende.IdentityModel is a .NET library for claims-based identity, OAuth 2.0 and OpenID
Connect. 

It provides an object model to interact with the endpoints defined in the various OAuth
and OpenId Connect specifications in the form of:
- types to represent the requests and responses
- extension methods to invoke requests
- constants defined in the specifications, such as standard scope, claim, and parameter
  names
- other convenience methods for performing common identity related operations

Duende.IdentityModel targets [.NET Standard 2.0](https://learn.microsoft.com/en-us/dotnet/standard/net-standard?tabs=net-standard-2-0),
making it suitable for .NET and .NET Framework.

For more documentation, please see the [documentation
site](https://docs.duendesoftware.com/foss).

## Related Packages

- Certified OIDC client library for native apps:
  [Duende.IdentityModel.OidcClient](https://www.nuget.org/packages/Duende.IdentityModel.OidcClient)
- Extensions for IdentityModel.OidcClient, including
  [DPoP](https://datatracker.ietf.org/doc/html/rfc9449) support:
  [Duende.IdentityModel.OidcClient.Extensions](https://www.nuget.org/packages/Duende.IdentityModel.OidcClient.DPoP)
- Authentication handler for introspection tokens: [IdentityModel.AspNetCore.OAuth2Introspection](https://www.nuget.org/packages/Duende.IdentityModel.AspNetCore.OAuth2Introspection)

## Feedback

Duende.IdentityModel is released as open source under the 
[Apache 2.0 license](https://github.com/duendesoftware/foss/blob/main/LICENSE). 
Bug reports and contributions are welcome at 
[the GitHub repository](https://github.com/duendesoftware/foss).
