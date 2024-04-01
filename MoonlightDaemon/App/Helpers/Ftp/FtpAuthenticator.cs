using System.Security.Claims;
using FubarDev.FtpServer.AccountManagement;
using MoonCore.Helpers;
using MoonCore.Services;
using MoonlightDaemon.App.Api.Moonlight.Requests.Ftp;
using MoonlightDaemon.App.Configuration;
using MoonlightDaemon.App.Exceptions;
using MoonlightDaemon.App.Extensions;
using MoonlightDaemon.App.Services;

namespace MoonlightDaemon.App.Helpers.Ftp;

public class FtpAuthenticator : IMembershipProviderAsync
{
    private readonly ServerService ServerService;
    private readonly FtpService FtpService;
    private readonly HttpApiClient<MoonlightException> HttpApiClient;

    public FtpAuthenticator(ServerService serverService, FtpService ftpService, HttpApiClient<MoonlightException> httpApiClient)
    {
        ServerService = serverService;
        FtpService = ftpService;
        HttpApiClient = httpApiClient;
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
            
            // Check if user reached the connections per user limit
            if (!await FtpService.RegisterSession(GetSessionIdentifier(realUsername, serverId.ToString())))
            {
                Logger.Debug($"{realUsername} triggered max connection limit");
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            // Check if the server is actually existent on this node
            var server = await ServerService.GetById(serverId);

            if (server == null)
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);

            // Build login model
            var loginData = new FtpLogin()
            {
                Password = password,
                Username = realUsername,
                ServerId = serverId
            };

            // Perform login request to panel. This will throw a MoonlightException if failed
            await HttpApiClient.Post("api/servers/ftp", loginData);

            // If we reach this point, login was successful and we want to return this state

            // Build (weird) identity stuff
            var userClaims = new List<Claim>
            {
                new("username", realUsername),
                new("serverId", serverId.ToString()),
                new("rootPath", server.Configuration.GetRuntimeVolumePath())
            };

            var identity = new ClaimsIdentity(userClaims);
            var principal = new ClaimsPrincipal(identity);
            
            Logger.Debug($"Login: {realUsername}");

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

    public async Task LogOutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = new())
    {
        var username = principal.Claims.First(x => x.Type == "username").Value;
        var serverId = principal.Claims.First(x => x.Type == "serverId").Value;
        
        Logger.Debug($"Logout: {username}");
        
        await FtpService.UnregisterSession(GetSessionIdentifier(username, serverId));
    }

    private string GetSessionIdentifier(string username, string serverId) => $"{username}.{serverId}";
}