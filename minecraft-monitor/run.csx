#r "NewtonSoft.Json"

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public static async void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"Minecraft monitor trigger function executed at: {DateTime.Now}");
    
    var minecraftEvent = GetMinecraftEvent();
    log.Info($"Minecraft event status: {minecraftEvent.status}");

    var customEventString = ConvertMinecraftEventToCustomEventString(minecraftEvent);
    log.Info($"Custom event to log: {customEventString}");
    
    var result = await PostCustomEventToLogAnalytics(customEventString);
    log.Info($"Result from Log Analytics: {result}");
}

public async static Task<string> PostCustomEventToLogAnalytics(string customEventString)
{
    var datestring = DateTime.UtcNow.ToString("r");
    var jsonBytes = Encoding.UTF8.GetBytes(customEventString);
    string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
    string hashedString = BuildSignature(stringToHash, sharedKey);
    string signature = "SharedKey " + customerId + ":" + hashedString;
    return await PostDataAsync(signature, datestring, customEventString);
}

public static string ConvertMinecraftEventToCustomEventString(MinecraftEvent minecraftEvent)
{
    var customEvent = new CustomEvent()
    {
        players_max = minecraftEvent.players.max,
        players_now = minecraftEvent.players.now,
        server_name = minecraftEvent.server.name
    };
    return JsonConvert.SerializeObject(customEvent);
}

public static MinecraftEvent GetMinecraftEvent()
{
    var request = WebRequest.Create ("https://mcapi.us/server/status?ip=13.70.121.148");
    var response = (HttpWebResponse)request.GetResponse();
    var dataStream = response.GetResponseStream ();
    var reader = new StreamReader (dataStream);
    var responseFromServer = reader.ReadToEnd ();
    reader.Close ();
    dataStream.Close ();
    response.Close ();
    return JsonConvert.DeserializeObject<MinecraftEvent>(responseFromServer);
}

public class CustomEvent
{
	public string players_max;
	public string players_now;
    public string server_name;
}

public class MinecraftEvent
{
	public string status;
	public bool online;
	public string motd;
	public string error;
	public PlayerInfo players;
	public ServerInfo server;
	public string last_online;
	public string last_updated;
	public string duration;
}

public class PlayerInfo
{
	public string max;
	public string now;
}

public class ServerInfo
{
	public string name;
	public Double protocol;
}

static string customerId = "f151a144-8bd9-4336-bd30-92db9958ee14";
static string sharedKey = "WIzIX9PB9kqLfiRcpm6jzfv49l95nXP++KV6LRFfIcvCC8R3gzPfqFgGhe0UquacJOsicDdv/K7RsJx+OStX+g==";
static string LogName = "MinecraftLog";
static string TimeStampField = "";

public static string BuildSignature(string message, string secret)
{
    var encoding = new System.Text.ASCIIEncoding();
    byte[] keyByte = Convert.FromBase64String(secret);
    byte[] messageBytes = encoding.GetBytes(message);
    using (var hmacsha256 = new HMACSHA256(keyByte))
    {
        byte[] hash = hmacsha256.ComputeHash(messageBytes);
        return Convert.ToBase64String(hash);
    }
}

public async static Task<string> PostDataAsync(string signature, string date, string json)
{
    try
    {
        string url = "https://" + customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

        System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Log-Type", LogName);
        client.DefaultRequestHeaders.Add("Authorization", signature);
        client.DefaultRequestHeaders.Add("x-ms-date", date);
        client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

        System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await client.PostAsync(new Uri(url), httpContent);

        return response.StatusCode.ToString();
    }
    catch (Exception excep)
    {
        return excep.Message;
    }
}

