using System.Text.Json.Serialization;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Serialization context used by the DPoP proof service and the client credential token cache.
/// </summary>
[JsonSerializable(typeof(ClientCredentialsToken))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(Dictionary<string, object>))]
public partial class DuendeAccessTokenSerializationContext : JsonSerializerContext
{
}
