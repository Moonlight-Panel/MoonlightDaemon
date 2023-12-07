using System.Text;
using Newtonsoft.Json;

namespace MoonlightDaemon.App.Extensions;

public static class HttpClientExtensions
{
    // This extension method handles api errors and throws them in an exception type which can be defined using the
    // second generic parameter. Exceptions like when connections issues occur will NOT be handled as
    // they should be handled by the ui
    public static async Task<T> SendHandled<T, TException>(this HttpClient client, HttpMethod method, string url,
        object? body = null, Action<Dictionary<string, string>>? headers = null) where TException : Exception
    {
        var request = new HttpRequestMessage(method, url);

        if (headers != null)
        {
            var headerDic = new Dictionary<string, string>();
            headers.Invoke(headerDic);

            foreach (var header in headerDic)
                request.Headers.Add(header.Key, header.Value);
        }

        if (body != null)
        {
            var jsonText = JsonConvert.SerializeObject(body);
            request.Content = new StringContent(jsonText, Encoding.UTF8, "application/json");
        }

        var result = await client.SendAsync(request);
        var resultBody = await result.Content.ReadAsStringAsync();

        if (!result.IsSuccessStatusCode)
        {
            var exception = Activator.CreateInstance(typeof(TException), resultBody) as Exception;
            throw exception!;
        }

        var resultObject = JsonConvert.DeserializeObject<T>(resultBody)!;

        return resultObject;
    }
    
    // This extension method handles api errors and throws them in an exception type which can be defined using the
    // second generic parameter. Exceptions like when connections issues occur will NOT be handled as
    // they should be handled by the ui
    public static async Task SendHandled<TException>(this HttpClient client, HttpMethod method, string url,
        object? body = null, Action<Dictionary<string, string>>? headers = null) where TException : Exception
    {
        var request = new HttpRequestMessage(method, url);

        if (headers != null)
        {
            var headerDic = new Dictionary<string, string>();
            headers.Invoke(headerDic);

            foreach (var header in headerDic)
                request.Headers.Add(header.Key, header.Value);
        }

        if (body != null)
        {
            var jsonText = JsonConvert.SerializeObject(body);
            request.Content = new StringContent(jsonText, Encoding.UTF8, "application/json");
        }

        var result = await client.SendAsync(request);
        var resultBody = await result.Content.ReadAsStringAsync();

        if (!result.IsSuccessStatusCode)
        {
            var exception = Activator.CreateInstance(typeof(TException), resultBody) as Exception;
            throw exception!;
        }
    }
}