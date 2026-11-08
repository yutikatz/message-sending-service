using Microsoft.EntityFrameworkCore;
using Sms.Api.Data;
using Sms.Api.Services;
using Xunit;

namespace Sms.Tests;

public class MessageServiceTests
{
    private static (MessageService service, AppDbContext db) Build(FakeSmsProvider provider)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options);
        return (new MessageService(db, provider), db);
    }

    [Fact]
    public async Task Successful_send_moves_pending_to_sent()
    {
        var (service, _) = Build(new FakeSmsProvider());
        var (message, created) = await service.CreateAndSendAsync("+972501234567", "שלום", null, default);

        Assert.True(created);
        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.NotNull(message.SentAt);
    }

    [Fact]
    public async Task Provider_rejection_is_recorded_as_failed()
    {
        var (service, _) = Build(new FakeSmsProvider { RejectNext = true });
        var (message, _) = await service.CreateAndSendAsync("+972501234567", "x", null, default);

        Assert.Equal(MessageStatus.Failed, message.Status);
        Assert.Equal("rejected", message.Error);
    }

    [Fact]
    public async Task Provider_exception_is_caught_and_recorded()
    {
        var (service, _) = Build(new FakeSmsProvider { ThrowNext = true });
        var (message, _) = await service.CreateAndSendAsync("+972501234567", "x", null, default);

        Assert.Equal(MessageStatus.Failed, message.Status);
        Assert.Contains("timeout", message.Error);
    }

    [Fact]
    public async Task Same_idempotency_key_creates_one_message_and_one_send()
    {
        var provider = new FakeSmsProvider();
        var (service, _) = Build(provider);

        var (first, createdFirst) = await service.CreateAndSendAsync("+972501234567", "x", "key-1", default);
        var (second, createdSecond) = await service.CreateAndSendAsync("+972501234567", "x", "key-1", default);

        Assert.True(createdFirst);
        Assert.False(createdSecond);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task A_non_pending_message_cannot_be_sent_again()
    {
        var (service, _) = Build(new FakeSmsProvider());
        var (message, _) = await service.CreateAndSendAsync("+972501234567", "x", null, default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(message, default));
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var (service, db) = Build(new FakeSmsProvider());

        await service.CreateAndSendAsync("+972501234567", "ok", null, default);
        db.Messages.Add(new Message { To = "+972501234567", Body = "x", Status = MessageStatus.Failed });
        await db.SaveChangesAsync();

        var sent = await service.ListAsync(MessageStatus.Sent, 0, 10, default);
        var all = await service.ListAsync(null, 0, 10, default);

        Assert.Single(sent);
        Assert.Equal(2, all.Count);
    }
}
