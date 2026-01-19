namespace DnDCampingManager.Api.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public List<JwtSigningKey> SigningKeys { get; set; } = new();
}

public class JwtSigningKey
{
    public string Kid { get; set; } = "";
    public string Key { get; set; } = "";
}
