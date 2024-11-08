## WPF Sample
This sample shows how to use Duende.IdentityModel.OidcClient to build a windows desktop
application in WPF. It uses [manual
mode](https://docs.duendesoftware.com/foss/identitymodel.oidcclient/manual/) with the
system browser, listens for callbacks on a private-use URI scheme, and protects the tokens
it obtains from replay attacks with DPoP.

### System Browser
The login flow opens the authorize endpoint using the system browser. We generally
recommend using the system browser for interactive protocol flows because it 
- creates a session that is shared with other applications and websites which gives the
end user a single sign on experience even between apps and websites
- keeps the user experience of logging in consistent which helps make phishing
attacks less likely to succeed
- allows for the user to use their password manager browser extensions

### Custom Schemes for callback uris
The callback urls use a custom scheme. Using a custom scheme avoids problems introduced by
firewalls or other security products blocking ports. Another good approach is to use
claimed https URIs. However, making that work in a sample is not easily possible, as
claimed URIs require configuration files on a domain indicating that relationship between
the app and domain. For more discussion of callback uris, please see [RFC 8252 - OAuth 2.0
for Native Apps](https://datatracker.ietf.org/doc/rfc8252/).

### DPoP
Finally, this sample uses the
[IdentityModel.OidcClient.Dpop](https://www.nuget.org/packages/IdentityModel.OidcClient.DPoP/)
extension package to implement [DPoP](https://datatracker.ietf.org/doc/rfc9449/). DPoP
defends against token replay by binding tokens to a secret key. In order to use those
tokens, the app must demonstrate proof of possession of that key. In this sample, the key
and stored session information are protected by the operating system using DPAPI before
they are stored.
