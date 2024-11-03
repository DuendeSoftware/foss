using PublicApiGenerator;

namespace Duende.IdentityModel.Verifications;

public class PublicApiVerificationTests
{
    [Fact]
    public async Task VerifyPublicApi()
    {
        var publicApi = typeof(JwtClaimTypes).Assembly.GeneratePublicApi();
        var settings = new VerifySettings();
        settings.UniqueForTargetFrameworkAndVersion();
        await Verify(publicApi, settings);
    }
}