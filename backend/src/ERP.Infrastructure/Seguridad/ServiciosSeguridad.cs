using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ERP.Application.Comun;
using ERP.Application.Seguridad;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Seguridad;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// PBKDF2-HMAC-SHA512 del framework. Formato: <c>pbkdf2-sha512$iteraciones$sal$hash</c> (base64).
/// El prefijo y las iteraciones viajan en el hash para poder endurecerlos sin invalidar los existentes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefijo = "pbkdf2-sha512";
    private const int Iteraciones = 210_000;
    private const int BytesSal = 16;
    private const int BytesClave = 32;

    private readonly Lazy<string> _senuelo;

    public Pbkdf2PasswordHasher() => _senuelo = new Lazy<string>(() => Hash(Guid.NewGuid().ToString()));

    public string HashSenuelo => _senuelo.Value;

    public string Hash(string password)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var clave = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA512, BytesClave);
        return $"{Prefijo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(clave)}";
    }

    public bool Verificar(string password, string hash)
    {
        var partes = hash.Split('$');
        if (partes.Length != 4 || partes[0] != Prefijo || !int.TryParse(partes[1], out var iteraciones) || iteraciones < 1)
            return false;

        try
        {
            var sal = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(password, sal, iteraciones, HashAlgorithmName.SHA512, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class OpcionesJwt
{
    /// <summary>Clave de firma HS256, mínimo 32 caracteres. Solo en User Secrets o variables de entorno.</summary>
    public string Clave { get; set; } = "";

    public string Emisor { get; set; } = "erp-api";

    public string Audiencia { get; set; } = "erp-frontend";

    public int MinutosAcceso { get; set; } = 15;

    public int DiasRefresh { get; set; } = 7;

    public SymmetricSecurityKey ClaveDeFirma()
    {
        if (Encoding.UTF8.GetByteCount(Clave) < 32)
            throw new InvalidOperationException(
                "Falta la clave de firma JWT (Jwt:Clave) o tiene menos de 32 caracteres. Configúrela en User Secrets o en la variable de entorno Jwt__Clave.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Clave));
    }
}

public static class ClaimsErp
{
    public const string Empresa = "emp";
    public const string Sucursal = "suc";
}

public sealed class JwtTokenService(OpcionesJwt opciones, IClock reloj) : ITokenService
{
    private readonly JsonWebTokenHandler _manejador = new();

    public TimeSpan VigenciaRefresh => TimeSpan.FromDays(opciones.DiasRefresh);

    /// <summary>
    /// El token lleva identidad y contexto de trabajo, nunca permisos: esos se resuelven en el
    /// servidor en cada solicitud.
    /// </summary>
    public TokenAcceso CrearAcceso(long usuarioId, string nombreUsuario, long empresaId, long sucursalId)
    {
        var ahora = reloj.UtcNow;
        var expira = ahora.AddMinutes(opciones.MinutosAcceso);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opciones.Emisor,
            Audience = opciones.Audiencia,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, nombreUsuario),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(ClaimsErp.Empresa, empresaId.ToString()),
                new Claim(ClaimsErp.Sucursal, sucursalId.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(opciones.ClaveDeFirma(), SecurityAlgorithms.HmacSha256),
        };
        return new TokenAcceso(_manejador.CreateToken(descriptor), expira);
    }

    public TokenOpaco CrearOpaco()
    {
        var valor = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new TokenOpaco(valor, Hash(valor));
    }

    public string Hash(string valor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}
