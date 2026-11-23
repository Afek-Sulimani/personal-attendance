using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace Personal.Attendance.Api.Authentication;

public sealed class MemoryCacheTicketStore(IMemoryCache memoryCache) : ITicketStore
{
    private readonly object _sessionLock = new();
    private const string CacheKeyPrefix = "personal:authentication-ticket:";
    private readonly ConcurrentDictionary<string, SessionIndexEntry> _sessionsByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Guid>> _sessionKeysByUser = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var expiresAt = ticket.Properties.ExpiresUtc ??
            throw new InvalidOperationException("Authentication tickets must have an expiration.");
        var sessionKey = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        StoreTicket(sessionKey, ticket, expiresAt);

        return Task.FromResult(sessionKey);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(ticket);

        var expiresAt = ticket.Properties.ExpiresUtc ??
            throw new InvalidOperationException("Authentication tickets must have an expiration.");

        StoreTicket(key, ticket, expiresAt);

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        memoryCache.TryGetValue(BuildCacheKey(key), out AuthenticationTicket? ticket);
        return Task.FromResult(ticket);
    }

    public Task RemoveAsync(string key)
    {
        lock (_sessionLock)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            RemoveSessionIndex(key);
            memoryCache.Remove(BuildCacheKey(key));
            return Task.CompletedTask;
        }
    }

    public Task<int> RemoveAllForUserAsync(Guid userId)
    {
        lock (_sessionLock)
        {
            if (userId == Guid.Empty || !_sessionKeysByUser.TryRemove(userId, out var sessions))
            {
                return Task.FromResult(0);
            }

            var removedCount = 0;
            foreach (var (sessionKey, registrationId) in sessions)
            {
                if (!_sessionsByKey.TryGetValue(sessionKey, out var indexedSession) ||
                    indexedSession.UserId != userId ||
                    indexedSession.RegistrationId != registrationId)
                {
                    continue;
                }

                if (RemoveExactSessionIndex(sessionKey, indexedSession))
                {
                    memoryCache.Remove(BuildCacheKey(sessionKey));
                    removedCount++;
                }
            }

            return Task.FromResult(removedCount);
        }
    }

    private void StoreTicket(string sessionKey, AuthenticationTicket ticket, DateTimeOffset expiresAt)
    {
        lock (_sessionLock)
        {
            var userId = Guid.TryParse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : Guid.Empty;
            var registrationId = Guid.NewGuid();
            var indexEntry = userId == Guid.Empty ? null : new SessionIndexEntry(userId, registrationId);

            if (indexEntry is not null)
            {
                if (_sessionsByKey.TryGetValue(sessionKey, out var previousEntry) && previousEntry.UserId != userId)
                {
                    RemoveSessionIndex(sessionKey);
                }

                _sessionsByKey[sessionKey] = indexEntry;
                _sessionKeysByUser
                    .GetOrAdd(userId, static _ => new ConcurrentDictionary<string, Guid>(StringComparer.Ordinal))
                    [sessionKey] = registrationId;
            }

            var cacheOptions = new MemoryCacheEntryOptions { AbsoluteExpiration = expiresAt };
            if (indexEntry is not null)
            {
                cacheOptions.RegisterPostEvictionCallback(
                    static (cacheKey, _, _, state) =>
                    {
                        var registration = (SessionEvictionRegistration)state!;
                        registration.Store.RemoveExactSessionIndex(registration.SessionKey, registration.Entry);
                    },
                    new SessionEvictionRegistration(this, sessionKey, indexEntry));
            }

            memoryCache.Set(BuildCacheKey(sessionKey), ticket, cacheOptions);
        }
    }

    private void RemoveSessionIndex(string sessionKey)
    {
        if (_sessionsByKey.TryGetValue(sessionKey, out var entry))
        {
            RemoveExactSessionIndex(sessionKey, entry);
        }
    }

    private bool RemoveExactSessionIndex(string sessionKey, SessionIndexEntry expectedEntry)
    {
        lock (_sessionLock)
        {
            var removed = ((ICollection<KeyValuePair<string, SessionIndexEntry>>)_sessionsByKey)
                .Remove(new KeyValuePair<string, SessionIndexEntry>(sessionKey, expectedEntry));
            if (!removed)
            {
                return false;
            }

            if (_sessionKeysByUser.TryGetValue(expectedEntry.UserId, out var userSessions))
            {
                ((ICollection<KeyValuePair<string, Guid>>)userSessions)
                    .Remove(new KeyValuePair<string, Guid>(sessionKey, expectedEntry.RegistrationId));
                if (userSessions.IsEmpty)
                {
                    ((ICollection<KeyValuePair<Guid, ConcurrentDictionary<string, Guid>>>)_sessionKeysByUser)
                        .Remove(new KeyValuePair<Guid, ConcurrentDictionary<string, Guid>>(
                            expectedEntry.UserId,
                            userSessions));
                }
            }

            return true;
        }
    }

    private static string BuildCacheKey(string sessionKey) => CacheKeyPrefix + sessionKey;

    private sealed record SessionIndexEntry(Guid UserId, Guid RegistrationId);

    private sealed record SessionEvictionRegistration(
        MemoryCacheTicketStore Store,
        string SessionKey,
        SessionIndexEntry Entry);
}
