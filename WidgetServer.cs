using System.Net;
using System.Text;
using System.Text.Json;

namespace TidalNowPlaying;

/// <summary>
/// Petit serveur HTTP local (aucune donnée ne sort de la machine) qui sert :
///  - GET /                -> le widget HTML à coller dans OBS (Browser Source)
///  - GET /now-playing.json -> les infos de lecture en cours
///  - GET /art              -> la pochette de l'album courant
/// </summary>
public class WidgetServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly int _port;
    private readonly string _wwwRoot;
    private TrackInfo _latest = new();
    private byte[]? _latestArt;
    private CancellationTokenSource? _cts;

    public WidgetServer(int port, string wwwRoot)
    {
        _port = port;
        _wwwRoot = wwwRoot;
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void UpdateTrack(TrackInfo info, byte[]? art)
    {
        _latest = info;
        if (art != null) _latestArt = art;
        if (!info.HasTrack) _latestArt = null;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener.Start();
        _ = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                break; // listener stopped
            }
            _ = Task.Run(() => HandleRequest(ctx));
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";

            // CORS simple pour laisser le widget tourner depuis OBS sans souci
            ctx.Response.AppendHeader("Access-Control-Allow-Origin", "*");

            switch (path)
            {
                case "/now-playing.json":
                    WriteJson(ctx);
                    break;

                case "/art":
                    if (_latestArt is { Length: > 0 })
                    {
                        ctx.Response.ContentType = SniffImageContentType(_latestArt);
                        ctx.Response.OutputStream.Write(_latestArt, 0, _latestArt.Length);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                    ctx.Response.OutputStream.Close();
                    break;

                case "/":
                case "/index.html":
                    ServeFile(ctx, Path.Combine(_wwwRoot, "index.html"), "text/html; charset=utf-8");
                    break;

                default:
                    ctx.Response.StatusCode = 404;
                    ctx.Response.OutputStream.Close();
                    break;
            }
        }
        catch
        {
            try { ctx.Response.OutputStream.Close(); } catch { /* noop */ }
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private void WriteJson(HttpListenerContext ctx)
    {
        // On extrapole la position à l'instant présent (entre deux polls de MediaWatcher)
        // pour que la barre de progression du widget avance en continu.
        var effectivePosition = _latest.PositionSeconds;
        if (_latest.IsPlaying)
        {
            var elapsed = (DateTime.UtcNow - _latest.CapturedAtUtc).TotalSeconds;
            if (elapsed > 0) effectivePosition += elapsed;
        }
        if (_latest.DurationSeconds > 0)
            effectivePosition = Math.Min(effectivePosition, _latest.DurationSeconds);

        var payload = new
        {
            title = _latest.Title,
            artist = _latest.Artist,
            album = _latest.Album,
            isPlaying = _latest.IsPlaying,
            positionSeconds = effectivePosition,
            durationSeconds = _latest.DurationSeconds,
            hasArt = _latest.HasArt,
            hasTrack = _latest.HasTrack
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    private static void ServeFile(HttpListenerContext ctx, string path, string contentType)
    {
        if (!File.Exists(path))
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.OutputStream.Close();
            return;
        }
        var bytes = File.ReadAllBytes(path);
        ctx.Response.ContentType = contentType;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    private static string SniffImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) return "image/jpeg";
        return "application/octet-stream";
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listener.Stop(); } catch { /* noop */ }
        try { _listener.Close(); } catch { /* noop */ }
    }
}
