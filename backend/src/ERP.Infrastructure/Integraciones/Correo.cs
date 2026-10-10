using ERP.Application.Comun;
using ERP.Application.Seguridad;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace ERP.Infrastructure.Integraciones;

public sealed class OpcionesCorreo
{
    /// <summary>"Archivo" guarda cada mensaje en disco (desarrollo); "Smtp" lo envía.</summary>
    public string Modo { get; set; } = "Archivo";

    /// <summary>Carpeta de salida del modo Archivo. Relativa a la carpeta de trabajo de la API.</summary>
    public string Carpeta { get; set; } = "correo-saliente";

    public string Remitente { get; set; } = "no-responder@erp.local";

    public string NombreRemitente { get; set; } = "ERP";

    public string Servidor { get; set; } = "";

    public int Puerto { get; set; } = 587;

    public string? Usuario { get; set; }

    public string? Password { get; set; }
}

/// <summary>
/// Adaptador de desarrollo: escribe el correo en un archivo de texto en vez de enviarlo.
/// El contenido (que puede incluir un enlace de recuperación) no se escribe en los logs.
/// </summary>
public sealed class CorreoArchivo(OpcionesCorreo opciones, IClock reloj, ILogger<CorreoArchivo> logger) : ICorreo
{
    public async Task EnviarAsync(string para, string asunto, string cuerpo, CancellationToken ct)
    {
        var carpeta = Path.GetFullPath(opciones.Carpeta);
        Directory.CreateDirectory(carpeta);

        var archivo = Path.Combine(carpeta, $"{reloj.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(archivo, $"Para: {para}\nAsunto: {asunto}\n\n{cuerpo}\n", ct);
        logger.LogInformation("Correo guardado en {Archivo} (modo Archivo, no se envió)", archivo);
    }
}

public sealed class CorreoSmtp(OpcionesCorreo opciones) : ICorreo
{
    public async Task EnviarAsync(string para, string asunto, string cuerpo, CancellationToken ct)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(opciones.NombreRemitente, opciones.Remitente));
        mensaje.To.Add(MailboxAddress.Parse(para));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("plain") { Text = cuerpo };

        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(opciones.Servidor, opciones.Puerto, SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrEmpty(opciones.Usuario))
            await cliente.AuthenticateAsync(opciones.Usuario, opciones.Password ?? "", ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);
    }
}
