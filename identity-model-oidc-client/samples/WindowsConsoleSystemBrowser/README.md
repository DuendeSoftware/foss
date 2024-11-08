## HttpSysConsoleClient Sample
This sample shows how to use Duende.IdentityModel.OidcClient to build a windows desktop
application in .NET 8. It uses [manual
mode](https://docs.duendesoftware.com/foss/identitymodel.oidcclient/manual/), and listens for
callbacks on a private-use URI scheme.

### Description of Implementation
This sample begins by adding an entry to the windows registry that will allow urls that
begin with the `sample-windows-client://` scheme to be handled by this sample application.
After that scheme is registered, it uses Duende.IdentityModel.OidcClient to create the
state and authorize url needed to begin the OIDC protocol. It opens the system browser at
the authorize url by calling `Process.Start` and passing the url. After the user
authenticates, the system browser redirects to the callback url using the
sample-windows-client. Since this sample has been registered for that url scheme, the
browser will start a new instance of the sample and pass its response to that second
instance. When the application starts up, it looks for a response parameter. When it sees
one, it processes that callback by using a Named Pipe to send the response back to the
original instance of the sample. The response is then passed back into
IdentityModel.OidcClient, which completes the flow by exchanging the authorization code in
the response for tokens. Finally, the sample displays those tokens on the console.
