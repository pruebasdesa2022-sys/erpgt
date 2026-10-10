using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Application.Organizacion;
using ERP.Domain.Comun;
using ERP.Domain.Seguridad;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Seguridad;

// ----- DTO -----

public sealed record LoginDto(string NombreUsuario, string Password);

public sealed record RefreshDto(string RefreshToken);

public sealed record CambiarContextoDto(string RefreshToken, long EmpresaId, long SucursalId);

public sealed record CambiarPasswordDto(string PasswordActual, string PasswordNueva);

public sealed record SolicitarRecuperacionDto(string Usuario);

public sealed record RestablecerPasswordDto(string Token, string PasswordNueva);

public sealed record TokensDto(string AccessToken, DateTime AccessExpiraEn, string RefreshToken, DateTime RefreshExpiraEn, bool DebeCambiarPassword);

public sealed record SesionDto(
    long UsuarioId,
    string NombreUsuario,
    string NombreCompleto,
    string Correo,
    bool DebeCambiarPassword,
    long EmpresaId,
    long SucursalId,
    IReadOnlyList<EmpresaResumen> Empresas,
    IReadOnlyList<SucursalResumen> Sucursales);

// ----- Validadores -----

public sealed class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.NombreUsuario).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PoliticaPassword.LongitudMaxima);
    }
}

public sealed class RefreshValidator : AbstractValidator<RefreshDto>
{
    public RefreshValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
}

public sealed class CambiarContextoValidator : AbstractValidator<CambiarContextoDto>
{
    public CambiarContextoValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmpresaId).GreaterThan(0);
        RuleFor(x => x.SucursalId).GreaterThan(0);
    }
}

public sealed class CambiarPasswordValidator : AbstractValidator<CambiarPasswordDto>
{
    public CambiarPasswordValidator()
    {
        RuleFor(x => x.PasswordActual).NotEmpty().MaximumLength(PoliticaPassword.LongitudMaxima);
        RuleFor(x => x.PasswordNueva).NotEmpty().MaximumLength(PoliticaPassword.LongitudMaxima);
    }
}

public sealed class SolicitarRecuperacionValidator : AbstractValidator<SolicitarRecuperacionDto>
{
    public SolicitarRecuperacionValidator() => RuleFor(x => x.Usuario).NotEmpty().MaximumLength(150);
}

public sealed class RestablecerPasswordValidator : AbstractValidator<RestablecerPasswordDto>
{
    public RestablecerPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PasswordNueva).NotEmpty().MaximumLength(PoliticaPassword.LongitudMaxima);
    }
}

// ----- Puertos -----

public sealed record UsuarioCredenciales
{
    public long Id { get; init; }
    public string NombreUsuario { get; init; } = "";
    public string Correo { get; init; } = "";
    public string NombreCompleto { get; init; } = "";
    public string PasswordHash { get; init; } = "";
    public bool DebeCambiarPassword { get; init; }
    public DateTime? PasswordCambiadoEn { get; init; }
    public int IntentosFallidos { get; init; }
    public DateTime? BloqueadoHasta { get; init; }
    public bool Activo { get; init; }
}

public sealed record AccesoEmpresa
{
    public long EmpresaId { get; init; }
    public bool EsPredeterminada { get; init; }
}

public sealed record AccesoSucursal
{
    public long SucursalId { get; init; }
    public bool EsPredeterminada { get; init; }
}

public sealed record AccesosUsuario(IReadOnlyList<AccesoEmpresa> Empresas, IReadOnlyList<AccesoSucursal> Sucursales);

public interface ICredencialesRepository
{
    /// <summary>Busca por nombre de usuario o correo. Dentro de una transacción bloquea la fila.</summary>
    Task<UsuarioCredenciales?> BuscarAsync(string nombreUsuarioOCorreo, CancellationToken ct);

    Task<UsuarioCredenciales?> ObtenerAsync(long id, CancellationToken ct);

    Task RegistrarFalloAsync(long id, int intentosFallidos, DateTime? bloqueadoHasta, CancellationToken ct);

    Task RegistrarExitoAsync(long id, DateTime ahoraUtc, CancellationToken ct);

    Task MarcarDebeCambiarAsync(long id, CancellationToken ct);

    /// <summary>Guarda el nuevo hash, lo agrega al historial y limpia intentos y bloqueo.</summary>
    Task CambiarPasswordAsync(long id, string hash, bool debeCambiar, DateTime ahoraUtc, long? modificadoPor, CancellationToken ct);

    Task<IReadOnlyList<string>> UltimosHashesAsync(long id, int cantidad, CancellationToken ct);

    Task<PoliticaPassword> ObtenerPoliticaAsync(CancellationToken ct);

    Task<AccesosUsuario> ObtenerAccesosAsync(long id, CancellationToken ct);
}

public sealed record NuevoRefreshToken(long UsuarioId, string FamiliaId, string TokenHash, long EmpresaId, long SucursalId, DateTime ExpiraEnUtc);

public sealed record RefreshGuardado
{
    public long Id { get; init; }
    public long UsuarioId { get; init; }
    public string FamiliaId { get; init; } = "";
    public long? EmpresaId { get; init; }
    public long? SucursalId { get; init; }
    public DateTime ExpiraEn { get; init; }
    public DateTime? RevocadoEn { get; init; }
}

public sealed record TokenRecuperacionGuardado
{
    public long Id { get; init; }
    public long UsuarioId { get; init; }
    public DateTime ExpiraEn { get; init; }
    public DateTime? UsadoEn { get; init; }
}

public static class MotivosRevocacion
{
    public const string Rotacion = "ROTACION";
    public const string Logout = "LOGOUT";
    public const string Reutilizacion = "REUTILIZACION";
    public const string CambioPassword = "CAMBIO_PASSWORD";
    public const string UsuarioInactivo = "USUARIO_INACTIVO";
    public const string Administrador = "ADMINISTRADOR";
}

public interface ISesionRepository
{
    Task<long> InsertarRefreshAsync(NuevoRefreshToken token, CancellationToken ct);

    /// <summary>Dentro de una transacción bloquea la fila, para que dos rotaciones simultáneas no pasen ambas.</summary>
    Task<RefreshGuardado?> ObtenerRefreshAsync(string tokenHash, CancellationToken ct);

    Task RevocarAsync(long id, string motivo, long? reemplazadoPorId, DateTime ahoraUtc, CancellationToken ct);

    Task RevocarFamiliaAsync(string familiaId, string motivo, DateTime ahoraUtc, CancellationToken ct);

    Task RevocarDeUsuarioAsync(long usuarioId, string motivo, DateTime ahoraUtc, CancellationToken ct);

    Task RegistrarIntentoAsync(string nombreUsuario, long? usuarioId, bool exitoso, string? motivo, CancellationToken ct);

    Task InsertarTokenRecuperacionAsync(long usuarioId, string tokenHash, DateTime expiraEnUtc, CancellationToken ct);

    Task<TokenRecuperacionGuardado?> ObtenerTokenRecuperacionAsync(string tokenHash, CancellationToken ct);

    Task MarcarTokenRecuperacionUsadoAsync(long id, DateTime ahoraUtc, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verificar(string password, string hash);

    /// <summary>Hash válido de una contraseña desconocida; se verifica contra él cuando el usuario no existe, para igualar tiempos.</summary>
    string HashSenuelo { get; }
}

public sealed record TokenAcceso(string Token, DateTime ExpiraEnUtc);

public sealed record TokenOpaco(string Valor, string Hash);

public interface ITokenService
{
    TokenAcceso CrearAcceso(long usuarioId, string nombreUsuario, long empresaId, long sucursalId);

    /// <summary>Token aleatorio de 256 bits y su SHA-256; en la base solo se guarda el hash.</summary>
    TokenOpaco CrearOpaco();

    string Hash(string valor);

    TimeSpan VigenciaRefresh { get; }
}

public interface ICorreo
{
    Task EnviarAsync(string para, string asunto, string cuerpo, CancellationToken ct);
}

public sealed class OpcionesSeguridad
{
    /// <summary>URL pública del frontend; con ella se arma el enlace de recuperación de contraseña.</summary>
    public string UrlFrontend { get; set; } = "http://localhost:3000";
}

// ----- Caso de uso -----

public sealed class AutenticacionService(
    ICredencialesRepository credenciales,
    ISesionRepository sesiones,
    IAutorizacionRepository autorizacion,
    IOrganizacionConsulta organizacion,
    IPasswordHasher hasher,
    ITokenService tokens,
    ICorreo correo,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj,
    OpcionesSeguridad opciones,
    ILogger<AutenticacionService> logger)
{
    private const string MensajeCredenciales = "Usuario o contraseña incorrectos.";

    public async Task<TokensDto> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var nombre = dto.NombreUsuario.Trim();

        // La transacción devuelve el resultado en vez de lanzar: el intento fallido y el
        // bloqueo deben quedar guardados, y una excepción los revertiría.
        var resultado = await unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var ahora = reloj.UtcNow;
            var usuario = await credenciales.BuscarAsync(nombre, ct);

            if (usuario is null || !usuario.Activo)
            {
                hasher.Verificar(dto.Password, hasher.HashSenuelo);
                return await FalloLoginAsync(nombre, usuario?.Id, "CREDENCIALES", "CREDENCIALES_INVALIDAS", MensajeCredenciales, ct);
            }

            if (usuario.BloqueadoHasta is { } hasta && hasta > ahora)
                return await FalloLoginAsync(nombre, usuario.Id, "BLOQUEADO", "CUENTA_BLOQUEADA",
                    "La cuenta está bloqueada temporalmente por intentos fallidos. Intente más tarde.", ct);

            var politica = await credenciales.ObtenerPoliticaAsync(ct);

            if (!hasher.Verificar(dto.Password, usuario.PasswordHash))
            {
                var intentos = usuario.IntentosFallidos + 1;
                var bloquear = intentos >= politica.MaxIntentosFallidos;
                await credenciales.RegistrarFalloAsync(usuario.Id, bloquear ? 0 : intentos,
                    bloquear ? ahora.AddMinutes(politica.BloqueoMinutos) : null, ct);
                if (bloquear)
                    await auditoria.RegistrarAccesoAsync(new EventoAcceso(EventosAcceso.Bloqueo, true, usuario.Id, nombre,
                        $"Bloqueo por {politica.MaxIntentosFallidos} intentos fallidos"), ct);
                return await FalloLoginAsync(nombre, usuario.Id, "CREDENCIALES", "CREDENCIALES_INVALIDAS", MensajeCredenciales, ct);
            }

            var contexto = await ResolverContextoAsync(usuario.Id, null, null, ct);
            if (contexto is null)
                return await FalloLoginAsync(nombre, usuario.Id, "SIN_ACCESOS", "SIN_ACCESOS",
                    "El usuario no tiene una empresa y sucursal asignadas. Contacte al administrador.", ct);

            var debeCambiar = usuario.DebeCambiarPassword;
            if (!debeCambiar && politica.Expirada(usuario.PasswordCambiadoEn, ahora))
            {
                await credenciales.MarcarDebeCambiarAsync(usuario.Id, ct);
                await autorizacion.IncrementarVersionAsync(ct);
                debeCambiar = true;
            }

            await credenciales.RegistrarExitoAsync(usuario.Id, ahora, ct);
            await sesiones.RegistrarIntentoAsync(nombre, usuario.Id, true, null, ct);
            await auditoria.RegistrarAccesoAsync(new EventoAcceso(EventosAcceso.LoginExitoso, true, usuario.Id, usuario.NombreUsuario), ct);

            var emitidos = await EmitirAsync(usuario, Guid.NewGuid().ToString(), contexto.Value.EmpresaId, contexto.Value.SucursalId, debeCambiar, ct);
            return new ResultadoSesion(emitidos.Tokens, null);
        }, ct);

        return resultado.Tokens ?? throw resultado.Error!;
    }

    public async Task<TokensDto> RefrescarAsync(RefreshDto dto, CancellationToken ct) =>
        await RotarAsync(dto.RefreshToken, null, null, EventosAcceso.Refresh, ct);

    public async Task<TokensDto> CambiarContextoAsync(CambiarContextoDto dto, CancellationToken ct) =>
        await RotarAsync(dto.RefreshToken, dto.EmpresaId, dto.SucursalId, EventosAcceso.CambioContexto, ct);

    public Task CerrarSesionAsync(RefreshDto dto, CancellationToken ct) =>
        unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var guardado = await sesiones.ObtenerRefreshAsync(tokens.Hash(dto.RefreshToken), ct);
            if (guardado is null)
                return;

            await sesiones.RevocarFamiliaAsync(guardado.FamiliaId, MotivosRevocacion.Logout, reloj.UtcNow, ct);
            await auditoria.RegistrarAccesoAsync(new EventoAcceso(EventosAcceso.Logout, true, guardado.UsuarioId, null), ct);
        }, ct);

    /// <summary>Cambia la contraseña del usuario autenticado y cierra todas sus sesiones.</summary>
    public Task CambiarPasswordAsync(CambiarPasswordDto dto, CancellationToken ct)
    {
        var usuarioId = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var usuario = await credenciales.ObtenerAsync(usuarioId, ct)
                ?? throw new NoAutenticadoException("SESION_INVALIDA", "La sesión ya no es válida.");

            if (!hasher.Verificar(dto.PasswordActual, usuario.PasswordHash))
                throw new ValidacionException("passwordActual", "La contraseña actual no es correcta.");

            await EstablecerPasswordAsync(usuario.Id, dto.PasswordNueva, "passwordNueva", usuarioId, ct);
            await auditoria.RegistrarAccesoAsync(new EventoAcceso(EventosAcceso.CambioPassword, true, usuario.Id, usuario.NombreUsuario), ct);
        }, ct);
    }

    /// <summary>
    /// Siempre termina igual, exista o no el usuario, para no revelar cuentas.
    /// El correo se envía después de confirmar la transacción.
    /// </summary>
    public async Task SolicitarRecuperacionAsync(SolicitarRecuperacionDto dto, CancellationToken ct)
    {
        var envio = await unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var usuario = await credenciales.BuscarAsync(dto.Usuario.Trim(), ct);
            if (usuario is null || !usuario.Activo)
            {
                await auditoria.RegistrarAccesoAsync(
                    new EventoAcceso(EventosAcceso.RecuperacionSolicitada, false, usuario?.Id, dto.Usuario.Trim(), "Usuario inexistente o inactivo"), ct);
                return ((string Correo, string Nombre, string Token, int Minutos)?)null;
            }

            var politica = await credenciales.ObtenerPoliticaAsync(ct);
            var token = tokens.CrearOpaco();
            await sesiones.InsertarTokenRecuperacionAsync(usuario.Id, token.Hash, reloj.UtcNow.AddMinutes(politica.RecuperacionMinutos), ct);
            await auditoria.RegistrarAccesoAsync(new EventoAcceso(EventosAcceso.RecuperacionSolicitada, true, usuario.Id, usuario.NombreUsuario), ct);
            return (usuario.Correo, usuario.NombreCompleto, token.Valor, politica.RecuperacionMinutos);
        }, ct);

        if (envio is not { } datos)
            return;

        var enlace = $"{opciones.UrlFrontend.TrimEnd('/')}/restablecer?token={Uri.EscapeDataString(datos.Token)}";
        var cuerpo =
            $"Hola {datos.Nombre}:\n\n" +
            "Recibimos una solicitud para restablecer su contraseña. Abra este enlace para elegir una nueva:\n\n" +
            $"{enlace}\n\n" +
            $"El enlace vence en {datos.Minutos} minutos y solo puede usarse una vez. " +
            "Si usted no hizo esta solicitud, ignore este mensaje.";

        try
        {
            await correo.EnviarAsync(datos.Correo, "Restablecer contraseña", cuerpo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No se informa al solicitante: la respuesta debe ser la misma en todos los casos.
            logger.LogError(ex, "No se pudo enviar el correo de recuperación de contraseña");
        }
    }

    public Task RestablecerPasswordAsync(RestablecerPasswordDto dto, CancellationToken ct) =>
        unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var ahora = reloj.UtcNow;
            var guardado = await sesiones.ObtenerTokenRecuperacionAsync(tokens.Hash(dto.Token), ct);
            if (guardado is null || guardado.UsadoEn is not null || guardado.ExpiraEn <= ahora)
                throw new ReglaNegocioException("TOKEN_INVALIDO", "El enlace de recuperación no es válido o ya venció. Solicite uno nuevo.");

            var usuario = await credenciales.ObtenerAsync(guardado.UsuarioId, ct);
            if (usuario is null || !usuario.Activo)
                throw new ReglaNegocioException("TOKEN_INVALIDO", "El enlace de recuperación no es válido o ya venció. Solicite uno nuevo.");

            await EstablecerPasswordAsync(usuario.Id, dto.PasswordNueva, "passwordNueva", null, ct);
            await sesiones.MarcarTokenRecuperacionUsadoAsync(guardado.Id, ahora, ct);
            await auditoria.RegistrarAccesoAsync(new EventoAcceso(EventosAcceso.RecuperacionUsada, true, usuario.Id, usuario.NombreUsuario), ct);
        }, ct);

    public async Task<SesionDto> ObtenerSesionAsync(CancellationToken ct)
    {
        var usuario = await credenciales.ObtenerAsync(actual.UsuarioIdRequerido(), ct)
            ?? throw new NoAutenticadoException("SESION_INVALIDA", "La sesión ya no es válida.");
        var (empresas, sucursales) = await AccesosActivosAsync(usuario.Id, ct);

        return new SesionDto(usuario.Id, usuario.NombreUsuario, usuario.NombreCompleto, usuario.Correo,
            usuario.DebeCambiarPassword, actual.EmpresaIdRequerida(), actual.SucursalId ?? 0, empresas, sucursales);
    }

    /// <summary>
    /// Valida complejidad e historial, guarda el hash y revoca todas las sesiones del usuario.
    /// Debe llamarse dentro de una transacción.
    /// </summary>
    internal async Task EstablecerPasswordAsync(long usuarioId, string passwordNueva, string campo, long? modificadoPor, CancellationToken ct, bool debeCambiar = false)
    {
        var politica = await credenciales.ObtenerPoliticaAsync(ct);
        var errores = politica.Validar(passwordNueva);
        if (errores.Count > 0)
            throw new ValidacionException(campo, [.. errores]);

        if (politica.HistorialCantidad > 0)
        {
            var anteriores = await credenciales.UltimosHashesAsync(usuarioId, politica.HistorialCantidad, ct);
            if (anteriores.Any(h => hasher.Verificar(passwordNueva, h)))
                throw new ValidacionException(campo, $"No puede reutilizar ninguna de sus últimas {politica.HistorialCantidad} contraseñas.");
        }

        var ahora = reloj.UtcNow;
        await credenciales.CambiarPasswordAsync(usuarioId, hasher.Hash(passwordNueva), debeCambiar, ahora, modificadoPor, ct);
        await sesiones.RevocarDeUsuarioAsync(usuarioId, MotivosRevocacion.CambioPassword, ahora, ct);
        await autorizacion.IncrementarVersionAsync(ct);
    }

    private async Task<TokensDto> RotarAsync(string refreshToken, long? empresaId, long? sucursalId, string evento, CancellationToken ct)
    {
        var resultado = await unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var ahora = reloj.UtcNow;
            var guardado = await sesiones.ObtenerRefreshAsync(tokens.Hash(refreshToken), ct);

            if (guardado is null)
                return Rechazo();

            if (guardado.RevocadoEn is not null)
            {
                // Un token ya rotado que vuelve a presentarse indica robo o copia: se cierra la sesión entera.
                await sesiones.RevocarFamiliaAsync(guardado.FamiliaId, MotivosRevocacion.Reutilizacion, ahora, ct);
                await auditoria.RegistrarAccesoAsync(
                    new EventoAcceso(EventosAcceso.RefreshReutilizado, false, guardado.UsuarioId, null, "Se revocó la sesión completa"), ct);
                return Rechazo();
            }

            if (guardado.ExpiraEn <= ahora)
                return Rechazo();

            var usuario = await credenciales.ObtenerAsync(guardado.UsuarioId, ct);
            if (usuario is null || !usuario.Activo)
            {
                await sesiones.RevocarFamiliaAsync(guardado.FamiliaId, MotivosRevocacion.UsuarioInactivo, ahora, ct);
                return Rechazo();
            }

            var cambio = empresaId is not null;
            var contexto = await ResolverContextoAsync(usuario.Id, empresaId ?? guardado.EmpresaId, sucursalId ?? guardado.SucursalId, ct, exigirExacto: cambio);
            if (contexto is null)
            {
                if (cambio)
                    return new ResultadoSesion(null, new AccesoDenegadoException("CONTEXTO_INVALIDO", "No tiene acceso a la empresa o sucursal seleccionada."));

                await sesiones.RevocarFamiliaAsync(guardado.FamiliaId, MotivosRevocacion.Administrador, ahora, ct);
                return Rechazo();
            }

            var emitidos = await EmitirAsync(usuario, guardado.FamiliaId, contexto.Value.EmpresaId, contexto.Value.SucursalId, usuario.DebeCambiarPassword, ct);
            await sesiones.RevocarAsync(guardado.Id, MotivosRevocacion.Rotacion, emitidos.RefreshId, ahora, ct);
            if (cambio)
                await auditoria.RegistrarAccesoAsync(new EventoAcceso(evento, true, usuario.Id, usuario.NombreUsuario,
                    $"Empresa {contexto.Value.EmpresaId}, sucursal {contexto.Value.SucursalId}"), ct);

            return new ResultadoSesion(emitidos.Tokens, null);
        }, ct);

        return resultado.Tokens ?? throw resultado.Error!;

        static ResultadoSesion Rechazo() =>
            new(null, new NoAutenticadoException("SESION_INVALIDA", "La sesión expiró. Inicie sesión de nuevo."));
    }

    private async Task<(TokensDto Tokens, long RefreshId)> EmitirAsync(
        UsuarioCredenciales usuario, string familiaId, long empresaId, long sucursalId, bool debeCambiar, CancellationToken ct)
    {
        var acceso = tokens.CrearAcceso(usuario.Id, usuario.NombreUsuario, empresaId, sucursalId);
        var refresh = tokens.CrearOpaco();
        var expira = reloj.UtcNow.Add(tokens.VigenciaRefresh);
        var id = await sesiones.InsertarRefreshAsync(new NuevoRefreshToken(usuario.Id, familiaId, refresh.Hash, empresaId, sucursalId, expira), ct);
        return (new TokensDto(acceso.Token, acceso.ExpiraEnUtc, refresh.Valor, expira, debeCambiar), id);
    }

    private async Task<ResultadoSesion> FalloLoginAsync(string nombre, long? usuarioId, string motivo, string errorCode, string mensaje, CancellationToken ct)
    {
        await sesiones.RegistrarIntentoAsync(nombre, usuarioId, false, motivo, ct);
        await auditoria.RegistrarAccesoAsync(new EventoAcceso(EventosAcceso.LoginFallido, false, usuarioId, nombre, motivo), ct);
        return new ResultadoSesion(null, new NoAutenticadoException(errorCode, mensaje));
    }

    private async Task<(IReadOnlyList<EmpresaResumen> Empresas, IReadOnlyList<SucursalResumen> Sucursales)> AccesosActivosAsync(long usuarioId, CancellationToken ct)
    {
        var accesos = await credenciales.ObtenerAccesosAsync(usuarioId, ct);
        var idsEmpresas = accesos.Empresas.Select(e => e.EmpresaId).ToHashSet();
        var idsSucursales = accesos.Sucursales.Select(s => s.SucursalId).ToHashSet();

        var empresas = (await organizacion.ListarEmpresasAsync(ct)).Where(e => idsEmpresas.Contains(e.Id)).ToList();
        var sucursales = (await organizacion.ListarSucursalesAsync(ct))
            .Where(s => idsSucursales.Contains(s.Id) && idsEmpresas.Contains(s.EmpresaId))
            .ToList();
        return (empresas, sucursales);
    }

    /// <summary>
    /// Elige empresa y sucursal de trabajo entre las activas a las que el usuario tiene acceso.
    /// Preferencia: la solicitada, luego la predeterminada, luego la primera. Con
    /// <paramref name="exigirExacto"/> solo se acepta exactamente la solicitada.
    /// </summary>
    private async Task<(long EmpresaId, long SucursalId)?> ResolverContextoAsync(
        long usuarioId, long? empresaPreferida, long? sucursalPreferida, CancellationToken ct, bool exigirExacto = false)
    {
        var accesos = await credenciales.ObtenerAccesosAsync(usuarioId, ct);
        var (empresas, sucursales) = await AccesosActivosAsync(usuarioId, ct);

        var empresa = empresas.FirstOrDefault(e => e.Id == empresaPreferida);
        if (empresa is null && !exigirExacto)
        {
            var predeterminada = accesos.Empresas.FirstOrDefault(e => e.EsPredeterminada)?.EmpresaId;
            empresa = empresas.FirstOrDefault(e => e.Id == predeterminada) ?? empresas.FirstOrDefault();
        }

        if (empresa is null)
            return null;

        var deEmpresa = sucursales.Where(s => s.EmpresaId == empresa.Id).ToList();
        var sucursal = deEmpresa.FirstOrDefault(s => s.Id == sucursalPreferida);
        if (sucursal is null && !exigirExacto)
        {
            var predeterminadas = accesos.Sucursales.Where(s => s.EsPredeterminada).Select(s => s.SucursalId).ToHashSet();
            sucursal = deEmpresa.FirstOrDefault(s => predeterminadas.Contains(s.Id)) ?? deEmpresa.FirstOrDefault();
        }

        return sucursal is null ? null : (empresa.Id, sucursal.Id);
    }

    private sealed record ResultadoSesion(TokensDto? Tokens, ErpException? Error);
}
