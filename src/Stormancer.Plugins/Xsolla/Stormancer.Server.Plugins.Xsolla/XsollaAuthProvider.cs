using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.Xsolla
{
    public class XsollaAuthProvider : IAuthenticationProvider
    {
        private readonly IUserSessions _sessions;
        private readonly IUserService _users;

        public XsollaAuthProvider(IUserSessions sessions, IUserService users)
        {
            _sessions = sessions;
            _users = users;
        }
        public string Type => "xsolla";

        public void AddMetadata(Dictionary<string, string> result)
        {
            result.Add("provider.xsolla", "enabled");
        }

        public Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            if (session.Identities.TryGetValue("xsolla", out var id))
            {
                status["xsolla"] = id;
            }
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
         
            throw new NotImplementedException();
        }

        public Task Unlink(User user)
        {
            return Task.CompletedTask;
        }
    }
}
