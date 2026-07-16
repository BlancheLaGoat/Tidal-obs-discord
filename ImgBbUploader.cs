using System.Text.Json;

namespace TidalNowPlaying;

/// <summary>
/// Upload une image vers ImgBB pour obtenir une URL publique, nécessaire pour
/// l'afficher dans le statut Discord (Imgur a fermé son API aux nouvelles
/// inscriptions fin 2025, on utilise donc ImgBB à la place - même principe).
/// </summary>
public static class ImgBbUploader
{
    private static readonly HttpClient Http = new();

    public static async Task<string?> UploadAsync(byte[] imageBytes, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("METS_TA_CLE_API"))
        {
            Logger.Log("ImgBB: pas de clé API configurée, pochette non uploadée.");
            return null;
        }

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new StringContent(Convert.ToBase64String(imageBytes)), "image" }
            };

            using var response = await Http.PostAsync($"https://api.imgbb.com/1/upload?key={apiKey}", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"ImgBB: échec upload ({(int)response.StatusCode}) - {body}");
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var success = doc.RootElement.TryGetProperty("success", out var successEl) && successEl.GetBoolean();
            if (!success)
            {
                Logger.Log($"ImgBB: réponse indiquant un échec - {body}");
                return null;
            }

            var link = doc.RootElement.GetProperty("data").GetProperty("url").GetString();
            Logger.Log($"ImgBB: pochette uploadée -> {link}");
            return link;
        }
        catch (Exception ex)
        {
            Logger.Log($"ImgBB: EXCEPTION lors de l'upload - {ex.Message}");
            return null;
        }
    }
}
