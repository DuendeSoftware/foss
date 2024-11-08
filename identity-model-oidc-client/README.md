## About Duende.IdentityModel.OidcClient.Extensions

Duende.IdentityModel.OidcClient.Extensions adds features to
Duende.IdentityModel.OidcClient that are implemented using more dependencies than are
required in the core Duende.IdentityModel.OidcClient package. Distributing these features
separately helps prevent certain transitive dependency problems.

The features added by this package include:
 - [DPoP](https://datatracker.ietf.org/doc/html/rfc9449) extensions for
   sender-constraining tokens.
 - Validation of Id Tokens implemented using the Microsoft JWT handler. This is usually
   not necessary, as id token signature validation is optional when using the code flow. 

## Samples
The WPF [sample](https://github.com/DuendeSoftware/foss/tree/main/identity-model-oidc-client/samples/wpf)  
in the [samples directory](https://github.com/DuendeSoftware/foss/tree/main/identity-model-oidc-client/samples)  
shows how to use Duende.IdentityModel.OidcClient.Extensions to implement DPoP.

## Documentation 

More documentation is available
[here](https://docs.duendesoftware.com/foss/identitymodel.oidcclient/).

## Certification
Duende.IdentityModel.OidcClient is a [certified](http://openid.net/certification/) OpenID
Connect relying party implementation.

## Feedback

Duende.IdentityModel.OidcClient is released as open source under the 
[Apache 2.0 license](https://github.com/DuendeSoftware/foss/blob/main/LICENSE). 
Bug reports and contributions are welcome at 
[the GitHub repository](https://github.com/DuendeSoftware/foss).
