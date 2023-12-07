using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PaliaLauncher;

public static class UpdateServer
{
    public static async Task<UpdateServerResponse> FetchChannelInfo(string bundle, string channel)
    {
        try
        {
            var client = new HttpClient();
            var response = await client.GetStringAsync($"{Configuration.DownloadServer}/bundle/{bundle}/channel/{channel}");
            var manifest = JsonConvert.DeserializeObject<UpdateServerResponse>(response);
            return manifest;
        }
        catch (Exception ex)
        {
            return new UpdateServerResponse
            {
                Ok = false,
                ResponseType = "error",
                Data = new UpdateServerResponse.ErrorResponse
                {
                    Message = ex.Message
                }
            };
        }
    }

    public static async Task<UpdateServerResponse> FetchManifest(string bundle, string version, string platform)
    {
        try
        {
            var client = new HttpClient();

            var resp = await client.GetAsync($"{Configuration.DownloadServer}/bundle/{bundle}/v/{version}/{platform}/manifest");
            if (resp.IsSuccessStatusCode)
            {
                return new UpdateServerResponse()
                {
                    Ok = true,
                    ResponseType = "",
                    Data = await resp.Content.ReadAsByteArrayAsync()
                };
            }

            return JsonConvert.DeserializeObject<UpdateServerResponse>(await resp.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return new UpdateServerResponse
            {
                Ok = false,
                ResponseType = "error",
                Data = new UpdateServerResponse.ErrorResponse
                {
                    Message = ex.Message
                }
            };
        }
    }
}

[JsonConverter(typeof(ResponseConverter))]
public class UpdateServerResponse
{
    public bool Ok { get; init; }
    public string ResponseType { get; set; }
    public object Data { get; init; }

    public class ChannelInfoResponse
    {
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("is_public")] public bool IsPublic { get; set; }
        [JsonProperty("bundle")] public string Bundle { get; set; }
        [JsonProperty("channel")] public string Channel { get; set; }
    }

    public class ErrorResponse
    {
        [JsonProperty("message")] public string Message { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }

    public class ManifestNotFoundErrorResponse : ErrorResponse
    {
        [JsonProperty("bundle")] public string Bundle { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("platform")] public string Platform { get; set; }

        public override string ToString()
        {
            return base.ToString() + $" (bundle: {Bundle}, version: {Version}, platform: {Platform})";
        }
    }

    private class ResponseConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var responseType = jsonObject["as"]?.Value<string>();

            var result = new UpdateServerResponse
            {
                Ok = jsonObject["ok"]?.Value<bool>() ?? false, ResponseType = responseType,
                Data = responseType switch
                {
                    "manifest_not_found" => jsonObject["v"]!.ToObject<UpdateServerResponse.ManifestNotFoundErrorResponse>(serializer),
                    "channel_info" => jsonObject["v"]!.ToObject<UpdateServerResponse.ChannelInfoResponse>(serializer),
                    "error" => jsonObject["v"]!.ToObject<UpdateServerResponse.ErrorResponse>(serializer),
                    _ => throw new JsonSerializationException("Unknown response type.")
                }
            };

            return result;
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }
}