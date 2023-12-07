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
            var response = await client.GetStringAsync($"https://dl.palia.com/bundle/{bundle}/channel/{channel}");
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

    public static byte[] FetchManifest(string bundle, string version, string platform)
    {
        try
        {
            var client = new HttpClient();
            var response = client.GetByteArrayAsync($"https://dl.palia.com/bundle/{bundle}/v/{version}/{platform}/manifest");
            return response.Result;
        }
        catch
        {
            return null;
        }
    }
}

[JsonConverter(typeof(ResponseConverter))]
public class UpdateServerResponse
{
    public bool Ok { get; set; }
    public string ResponseType { get; set; }
    public object Data { get; set; }

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