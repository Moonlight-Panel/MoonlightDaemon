using System.Security.Claims;
using FubarDev.FtpServer.AccountManagement;
using MoonlightDaemon.App.Api.Moonlight.Requests.Ftp;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Helpers.Ftp;

public class FtpAuthenticator : IMembershipProviderAsync
{
    private readonly ServerService ServerService;
    private readonly HttpClient HttpClient;

    public FtpAuthenticator(ConfigService configService, ServerService serverService)
    {
        ServerService = serverService;

        HttpClient = new()
        {
            BaseAddress = new Uri(configService.Get().Remote.Url + "api/ftp/")
        };

        HttpClient.DefaultRequestHeaders.Add("Authorization", configService.Get().Remote.Token);
    }

    public async Task<MemberValidationResult> ValidateUserAsync(string username, string password)
    {
        try
        {
            // Parse username
            var parts = username.Split(".");

            // This will most likely occur when a bot finds this ftp server
            // and tries a dictionary attack or similar bruteforce attacks
            if (parts.Length != 2)
            {
                Logger.Warn($"Invalid username format received. Username: '{username}', Password: '{password}'");
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            var realUsername = parts[0];
            var serverId = int.Parse(parts[1]);

            // Check if the server is actually existent on this node
            var server = await ServerService.GetById(serverId);

            if (server == null)
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);

            // Build login model
            var loginData = new Login()
            {
                Password = password,
                Username = realUsername,
                IpAddress = "N/A",
                ResourceId = serverId,
                ResourceType = "server"
            };

            // Perform login request to panel. This will throw a MoonlightException if failed
            await HttpClient.SendHandled<MoonlightException>(HttpMethod.Post, "login", loginData);

            // If we reach this point, login was successful and we want to return this state

            // Build (weird) identity stuff
            var userClaims = new List<Claim>
            {
                new("username", username),
                new("serverId", serverId.ToString()),
                new("rootPath", server.Configuration.GetRuntimeVolumePath())
            };

            var identity = new ClaimsIdentity(userClaims);
            var principal = new ClaimsPrincipal(identity);

            return new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, principal);
        }
        catch (MoonlightException)
        {
            Logger.Warn($"Failed ftp login for user '{username}'");
            
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }
        catch (Exception e)
        {
            Logger.Error("An unhandled error occured performing login");
            Logger.Error(e);

            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }
    }

    public async Task<MemberValidationResult> ValidateUserAsync(string username, string password,
        CancellationToken _ = new()) => await ValidateUserAsync(username, password);

    public Task LogOutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }
}