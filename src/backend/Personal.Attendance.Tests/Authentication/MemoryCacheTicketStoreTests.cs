using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Personal.Attendance.Api.Authentication;

namespace Personal.Attendance.Tests.Authentication;

[TestClass]
public sealed class MemoryCacheTicketStoreTests
{
    private static readonly DateTimeOffset ExpiresAtUtc = DateTimeOffset.Parse("2099-01-01T00:00:00Z");

    [TestMethod]
    public async Task RemoveAllForUserAsync_RevokesEverySessionForThatUserOnly()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryCacheTicketStore(cache);
        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var targetSessionOne = await store.StoreAsync(CreateTicket(targetUserId));
        var targetSessionTwo = await store.StoreAsync(CreateTicket(targetUserId));
        var otherSession = await store.StoreAsync(CreateTicket(otherUserId));

        var removedCount = await store.RemoveAllForUserAsync(targetUserId);

        Assert.AreEqual(2, removedCount);
        Assert.IsNull(await store.RetrieveAsync(targetSessionOne));
        Assert.IsNull(await store.RetrieveAsync(targetSessionTwo));
        Assert.IsNotNull(await store.RetrieveAsync(otherSession));
        Assert.AreEqual(0, await store.RemoveAllForUserAsync(targetUserId));
    }

    [TestMethod]
    public async Task ConcurrentRegistrationAndRemoval_KeepSurvivingTicketsRevocable()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryCacheTicketStore(cache);
        var user = Guid.NewGuid();
        var keys = new System.Collections.Concurrent.ConcurrentBag<string>();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
        {
            var key = await store.StoreAsync(CreateTicket(user));
            keys.Add(key);
            await store.RemoveAsync(key);
            keys.Add(await store.StoreAsync(CreateTicket(user)));
        })));
        await store.RemoveAllForUserAsync(user);
        foreach (var key in keys) Assert.IsNull(await store.RetrieveAsync(key));
    }

    private static AuthenticationTicket CreateTicket(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "test");
        var properties = new AuthenticationProperties { ExpiresUtc = ExpiresAtUtc };
        return new AuthenticationTicket(new ClaimsPrincipal(identity), properties, "test");
    }
}
