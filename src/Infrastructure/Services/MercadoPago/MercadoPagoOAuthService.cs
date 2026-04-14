using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using TurneroApp.Application.DTOs.MercadoPago;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Services.MercadoPago;

public sealed class MercadoPagoOAuthService : IMercadoPagoOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly MercadoPagoOAuthOptions _options;
    private readonly ISucursalMercadoPagoRepository _repo;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MercadoPagoOAuthService(
        HttpClient httpClient,
        IOptions<MercadoPagoOAuthOptions> options,
        ISucursalMercadoPagoRepository repo)
    {
        _httpClient = httpClient;
        _options    = options.Value;
        _repo       = repo;
    }

    /// <inheritdoc/>
    public string GenerarUrlAutorizacion(Guid sucursalId)
    {
        // state = sucursalId en formato "N" (sin guiones) para usar como correlación
        var state = sucursalId.ToString("N");

        var query = new Dictionary<string, string?>
        {
            ["client_id"]     = _options.ClientId,
            ["response_type"] = "code",
            ["platform_id"]   = "mp",
            ["state"]         = state,
            ["redirect_uri"]  = _options.RedirectUri,
        };

        return QueryHelpers.AddQueryString(_options.AuthorizationBaseUrl, query!);
    }

    /// <inheritdoc/>
    public async Task ConectarAsync(Guid sucursalId, string code)
    {
        var formParams = new Dictionary<string, string>
        {
            ["client_id"]     = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = _options.RedirectUri,
        };
        var token = await PostTokenAsync(formParams);

        var ahora     = DateTime.UtcNow;
        var existente = await _repo.GetBySucursalIdAsync(sucursalId);

        if (existente is null)
        {
            await _repo.AddAsync(new SucursalMercadoPago
            {
                Id                      = Guid.NewGuid(),
                SucursalId              = sucursalId,
                MpUserId                = token.UserId.ToString(),
                AccessToken             = token.AccessToken,
                RefreshToken            = token.RefreshToken,
                AccessTokenExpiraEnUtc  = ahora.AddSeconds(token.ExpiresIn),
                RefreshTokenExpiraEnUtc = ahora.AddMonths(6),
                FechaConexionUtc        = ahora,
                UltimaActualizacionUtc  = ahora,
                Activo                  = true,
            });
        }
        else
        {
            existente.MpUserId                = token.UserId.ToString();
            existente.AccessToken             = token.AccessToken;
            existente.RefreshToken            = token.RefreshToken;
            existente.AccessTokenExpiraEnUtc  = ahora.AddSeconds(token.ExpiresIn);
            existente.RefreshTokenExpiraEnUtc = ahora.AddMonths(6);
            existente.UltimaActualizacionUtc  = ahora;
            existente.Activo                  = true;
            _repo.Update(existente);
        }

        await _repo.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<string> ObtenerAccessTokenVigenteAsync(Guid sucursalId)
    {
        var registro = await _repo.GetBySucursalIdAsync(sucursalId)
            ?? throw new InvalidOperationException(
                "La sucursal no tiene una cuenta de Mercado Pago conectada.");

        // Refrescar si expira en menos de 5 minutos
        if (registro.AccessTokenExpiraEnUtc <= DateTime.UtcNow.AddMinutes(5))
            await RefrescarTokenAsync(sucursalId);

        // Releer tras el refresh
        registro = await _repo.GetBySucursalIdAsync(sucursalId)
            ?? throw new InvalidOperationException(
                "No se pudo recuperar el token actualizado.");

        return registro.AccessToken;
    }

    /// <inheritdoc/>
    public async Task RefrescarTokenAsync(Guid sucursalId)
    {
        var registro = await _repo.GetBySucursalIdAsync(sucursalId)
            ?? throw new InvalidOperationException(
                "La sucursal no tiene una cuenta de Mercado Pago conectada.");

        var formParams = new Dictionary<string, string>
        {
            ["client_id"]     = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = registro.RefreshToken,
            ["redirect_uri"]  = _options.RedirectUri,
        };

        var token = await PostTokenAsync(formParams);

        var ahora = DateTime.UtcNow;
        registro.AccessToken             = token.AccessToken;
        registro.RefreshToken            = token.RefreshToken;
        registro.AccessTokenExpiraEnUtc  = ahora.AddSeconds(token.ExpiresIn);
        registro.RefreshTokenExpiraEnUtc = ahora.AddMonths(6);
        registro.UltimaActualizacionUtc  = ahora;

        _repo.Update(registro);
        await _repo.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<MercadoPagoEstadoDto> ObtenerEstadoAsync(Guid sucursalId)
    {
        var registro = await _repo.GetBySucursalIdAsync(sucursalId);

        if (registro is null || !registro.Activo)
            return new MercadoPagoEstadoDto(false, null, null);

        return new MercadoPagoEstadoDto(true, registro.MpUserId, registro.FechaConexionUtc);
    }

    /// <inheritdoc/>
    public async Task DesconectarAsync(Guid sucursalId)
    {
        var registro = await _repo.GetBySucursalIdAsync(sucursalId)
            ?? throw new InvalidOperationException("La sucursal no tiene una cuenta de Mercado Pago conectada.");

        _repo.Delete(registro);
        await _repo.SaveChangesAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Envía un POST a TokenUrl con content-type: application/x-www-form-urlencoded
    /// y accept: application/json, tal como lo exige la doc de Mercado Pago.
    /// </summary>
    private async Task<MercadoPagoOAuthTokenResponse> PostTokenAsync(
        Dictionary<string, string> formParams)
    {
        if (_options.TestMode)
            formParams["test_token"] = "true";

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(formParams),
        };
        request.Headers.Add("accept", "application/json");

        using var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Mercado Pago OAuth error {(int)response.StatusCode}: {content}");

        return JsonSerializer.Deserialize<MercadoPagoOAuthTokenResponse>(content, _jsonOptions)
            ?? throw new InvalidOperationException(
                "Mercado Pago devolvió una respuesta inválida al intercambiar el token.");
    }
}
