using MoonlightDaemon.App.Services;
using Newtonsoft.Json;
using RestSharp;

namespace MoonlightDaemon.App.ApiClients.Moonlight;

public class MoonlightApiHelper
{
    private readonly RestClient Client;
    private readonly WingsConfigService WingsConfigService;

    public MoonlightApiHelper(WingsConfigService wingsConfigService)
    {
        WingsConfigService = wingsConfigService;
        Client = new();
    }
    
    public async Task<T> Get<T>(string resource)
    {
        var request = await CreateRequest(resource);

        request.Method = Method.Get;
        
        var response = await Client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            if (response.StatusCode != 0)
            {
                throw new Exception(
                    $"An error occured: ({response.StatusCode}) {response.Content}"
                );
            }
            else
            {
                throw new Exception($"An internal error occured: {response.ErrorMessage}");
            }
        }

        return JsonConvert.DeserializeObject<T>(response.Content!)!;
    }
    
    public async Task Post(string resource, object body)
    {
        var request = await CreateRequest(resource);

        request.Method = Method.Post;

        request.AddParameter("text/plain", JsonConvert.SerializeObject(body), ParameterType.RequestBody);

        var response = await Client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            if (response.StatusCode != 0)
            {
                throw new Exception(
                    $"An error occured: ({response.StatusCode}) {response.Content}"
                );
            }
            else
            {
                throw new Exception($"An internal error occured: {response.ErrorMessage}");
            }
        }
    }
    
    public async Task Delete(string resource, object body)
    {
        var request = await CreateRequest(resource);

        request.Method = Method.Delete;

        request.AddParameter("text/plain", JsonConvert.SerializeObject(body), ParameterType.RequestBody);

        var response = await Client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            if (response.StatusCode != 0)
            {
                throw new Exception(
                    $"An error occured: ({response.StatusCode}) {response.Content}"
                );
            }
            else
            {
                throw new Exception($"An internal error occured: {response.ErrorMessage}");
            }
        }
    }

    private Task<RestRequest> CreateRequest(string resource)
    {
        var url = $"{WingsConfigService.Remote}/";
        
        RestRequest request = new(url + resource);

        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {WingsConfigService.Id}.{WingsConfigService.Token}");
        
        return Task.FromResult(request);
    }
}