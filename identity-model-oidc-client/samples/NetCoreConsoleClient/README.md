## NetCoreConsoleClient Sample
This sample shows how to use Duende.IdentityModel.OidcClient to build a cross platform desktop
application in .NET 8. It uses [automatic
mode](https://docs.duendesoftware.com/foss/identitymodel.oidcclient/automatic/), where an
IBrowser implementation describes how to invoke the system browser. This sample's browser
implementation knows how to open the system browser on Windows, Mac, and Linux (see
`OpenBrowser`), and listens for callback requests using the cross platform kestrel web
server. See SystemBrowser.cs for the full IBrowser implementation details.

### Description of Implementation
This sample first finds an unused port to use as a callback url where it sets up a minimal
kestrel web server listening for callbacks from the identity provider. 

Then it uses Duende.IdentityModel.OidcClient to begin the login process, passing an
instance of the SystemBrowser. Behind the scenes, Duende.IdentityModel.OidcClient prepares
the state, generates the authorization url, and opens the browser there. Once the user
authenticates, the identity provider redirects to the callback url, where kestrel is
listening. Kestrel does two things: it both renders a simple response to the browser with
a message telling the user to return to the app, and it passes the callback request back
into Duende.IdentityModel.OidcClient, which completes the Oidc flow by exchanging the
authorization code for tokens.
