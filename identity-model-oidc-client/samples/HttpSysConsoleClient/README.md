## HttpSysConsoleClient Sample
This sample shows how to use Duende.IdentityModel.OidcClient to build a windows desktop
application in .NET 8. It uses [manual
mode](https://docs.duendesoftware.com/foss/identitymodel.oidcclient/manual/), and listens
for callbacks on the loopback address with a hard-coded port.

### Description of Implementation
This sample first creates an
[HttpListener](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-net-httplistener)
to wait for callbacks from the identity provider at a hard-coded port on the local
machine. Then it usesDuende.IdentityModel.OidcClient to prepare the state and url needed to
login, using that callback url as the redirect uri. It then opens the system browser to
the authorize url by calling `Process.Start` and passing the url. After the user
authenticates, the system browser redirects to the callback url. The HttpListener receives
that request and then does two things: it both renders a simple response to the browser
with a message telling the user to return to the app, and it reads the authorization code
from the request. The authorization code is then passed intoDuende.IdentityModel.OidcClient,
exchanging the code for tokens. Finally, the sample displays those tokens on the console
and cleans up its HttpListener.

### HTTP.sys
HttpListener is built on top of the HTTP.sys, the windows driver that handles http
requests. 
