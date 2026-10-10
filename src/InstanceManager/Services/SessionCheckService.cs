using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public interface ISessionChecker
{
    Task<SessionStatus> CheckAsync(Account account, CancellationToken cancellationToken = default);
}

public sealed class SessionCheckService : ISessionChecker
{
    private readonly DpapiSecureStore _secure;
    private readonly RobloxAuthService _auth;

    public SessionCheckService(DpapiSecureStore secure, RobloxAuthService auth)
    {
        _secure = secure;
        _auth = auth;
    }

    public Task<SessionStatus> CheckAsync(Account account, CancellationToken cancellationToken = default) =>
        _secure.TryUnprotect(account.EncryptedCookie, out string cookie)
            ? _auth.CheckSessionAsync(cookie, account.UserId, cancellationToken)
            : Task.FromResult(SessionStatus.Expired);
}
