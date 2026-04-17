using System;
using ChattModels;
using Xunit;

public class MessageSerializationTests
{
    [Fact]
    public void TextMessage_SerializeDeserialize_RetainsContent()
    {
        var m = new TextMessage("Alice", "Hello world");
        var json = m.ToJson();
        var parsed = MessageBase.FromJson(json) as TextMessage;
        Assert.NotNull(parsed);
        Assert.Equal(m.Sender, parsed.Sender);
        Assert.Equal(m.Content, parsed.Content);
    }

    [Fact]
    public void PrivateMessage_SerializeDeserialize_RetainsRecipient()
    {
        var m = new PrivateMessage("Alice", "Bob", "Secret");
        var json = m.ToJson();
        var parsed = MessageBase.FromJson(json) as PrivateMessage;
        Assert.NotNull(parsed);
        Assert.Equal(m.Sender, parsed.Sender);
        Assert.Equal(m.Recipient, parsed.Recipient);
        Assert.Equal(m.Content, parsed.Content);
    }
}
