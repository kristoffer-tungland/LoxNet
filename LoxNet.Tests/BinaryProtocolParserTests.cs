using System;
using System.Text;
using Xunit;

namespace LoxNet.Tests;

public class BinaryProtocolParserTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Builds a complete Loxone binary message (header + payload).</summary>
    private static byte[] BuildMessage(LoxoneMessageType type, byte[] payload)
    {
        var buf = new byte[8 + payload.Length];
        buf[0] = 0x03; // magic
        buf[1] = (byte)type;
        buf[2] = 0x00; // info flags
        buf[3] = 0x00; // reserved
        BitConverter.GetBytes((uint)payload.Length).CopyTo(buf, 4);
        payload.CopyTo(buf, 8);
        return buf;
    }

    /// <summary>Encodes a 128-bit Loxone UUID from its canonical string form into 16 LE bytes.</summary>
    private static byte[] UuidToBytes(string uuidStr)
    {
        // Re-add the 5th dash that Loxone omits so Guid can parse it
        // Loxone format: "aabbccdd-eeff-gghh-iijjkkllmmnnoopp" (no 5th group)
        // Standard Guid: "aabbccdd-eeff-gghh-iijj-kkllmmnnoopp"
        string standardForm;
        var parts = uuidStr.Split('-');
        if (parts.Length == 4 && parts[3].Length == 16)
        {
            standardForm = $"{parts[0]}-{parts[1]}-{parts[2]}-{parts[3].Substring(0, 4)}-{parts[3].Substring(4)}";
        }
        else
        {
            standardForm = uuidStr;
        }
        return new Guid(standardForm).ToByteArray(); // ToByteArray() is little-endian
    }

    /// <summary>Builds one value-state record: 16-byte LE UUID + 8-byte LE double.</summary>
    private static byte[] ValueRecord(string uuid, double value)
    {
        var buf = new byte[24];
        UuidToBytes(uuid).CopyTo(buf, 0);
        BitConverter.GetBytes(value).CopyTo(buf, 16);
        return buf;
    }

    // ── Header parsing ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseMessage_ReturnsFalse_WhenBufferTooShort()
    {
        var result = BinaryProtocolParser.TryParseMessage(new byte[4], out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseMessage_ReturnsFalse_WhenMagicByteWrong()
    {
        var data = new byte[16];
        data[0] = 0x01; // wrong magic
        var result = BinaryProtocolParser.TryParseMessage(data, out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseMessage_ReturnsFalse_WhenPayloadNotYetFullyReceived()
    {
        // Header says payload = 100 bytes but buffer only has 50 bytes
        var data = new byte[8 + 50];
        data[0] = 0x03;
        data[1] = (byte)LoxoneMessageType.Text;
        BitConverter.GetBytes(100u).CopyTo(data, 4);
        var result = BinaryProtocolParser.TryParseMessage(data, out _, out _);
        Assert.False(result);
    }

    // ── Text messages ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseMessage_TextMessage_ReturnsParsedText()
    {
        const string json = "{\"LL\":{\"Code\":\"200\",\"value\":\"ok\"}}";
        var payload = Encoding.UTF8.GetBytes(json);
        var data = BuildMessage(LoxoneMessageType.Text, payload);

        var ok = BinaryProtocolParser.TryParseMessage(data, out var msg, out var len);

        Assert.True(ok);
        Assert.NotNull(msg);
        Assert.Equal(LoxoneMessageType.Text, msg!.MessageType);
        Assert.Equal(json, msg.Text);
        Assert.Empty(msg.StateEvents);
        Assert.Equal(data.Length, len);
    }

    [Fact]
    public void TryParseMessage_TextMessage_ReportsCorrectTotalLength()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var data = BuildMessage(LoxoneMessageType.Text, payload);

        BinaryProtocolParser.TryParseMessage(data, out _, out var len);
        Assert.Equal(8 + payload.Length, len);
    }

    // ── Value-state event table ───────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseMessage_ValueStates_DecodesUuidsAndValues()
    {
        const string uuid1 = "aabbccdd-eeff-0011-2233445566778899";
        const string uuid2 = "11223344-5566-7788-99aabbccddeeff00";

        var record1 = ValueRecord(uuid1, 1.0);
        var record2 = ValueRecord(uuid2, 42.5);

        var payload = new byte[record1.Length + record2.Length];
        record1.CopyTo(payload, 0);
        record2.CopyTo(payload, record1.Length);

        var data = BuildMessage(LoxoneMessageType.ValueStates, payload);
        var ok = BinaryProtocolParser.TryParseMessage(data, out var msg, out _);

        Assert.True(ok);
        Assert.NotNull(msg);
        Assert.Equal(LoxoneMessageType.ValueStates, msg!.MessageType);
        Assert.Null(msg.Text);
        Assert.Equal(2, msg.StateEvents.Count);

        Assert.Equal(uuid1, msg.StateEvents[0].Uuid);
        Assert.Equal("1", msg.StateEvents[0].Value);

        Assert.Equal(uuid2, msg.StateEvents[1].Uuid);
        Assert.Equal("42.5", msg.StateEvents[1].Value);
    }

    [Fact]
    public void TryParseMessage_ValueStates_EmptyPayload_ReturnsNoEvents()
    {
        var data = BuildMessage(LoxoneMessageType.ValueStates, Array.Empty<byte>());
        var ok = BinaryProtocolParser.TryParseMessage(data, out var msg, out _);

        Assert.True(ok);
        Assert.NotNull(msg);
        Assert.Empty(msg!.StateEvents);
    }

    // ── Text-state event table ────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseMessage_TextStates_DecodesTextValue()
    {
        const string uuid = "aabbccdd-eeff-0011-2233445566778899";
        const string text = "Hello World";
        var textBytes = Encoding.UTF8.GetBytes(text);

        // Record layout: uuid(16) + iconuuid(16) + textlen(4) + text(N) padded to 4 bytes
        var uuidBytes = UuidToBytes(uuid);
        var iconBytes = new byte[16]; // all zeros
        var textLen = BitConverter.GetBytes((uint)textBytes.Length);

        int rawLen = 16 + 16 + 4 + textBytes.Length;
        int paddedLen = ((rawLen - 1) / 4 + 1) * 4;
        var payload = new byte[paddedLen];

        int pos = 0;
        uuidBytes.CopyTo(payload, pos); pos += 16;
        iconBytes.CopyTo(payload, pos); pos += 16;
        textLen.CopyTo(payload, pos); pos += 4;
        textBytes.CopyTo(payload, pos);

        var data = BuildMessage(LoxoneMessageType.TextStates, payload);
        var ok = BinaryProtocolParser.TryParseMessage(data, out var msg, out _);

        Assert.True(ok);
        Assert.NotNull(msg);
        Assert.Equal(LoxoneMessageType.TextStates, msg!.MessageType);
        Assert.Single(msg.StateEvents);
        Assert.Equal(uuid, msg.StateEvents[0].Uuid);
        Assert.Equal(text, msg.StateEvents[0].Value);
    }

    // ── Multi-message buffer ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseMessage_ReturnsFirstMessageLength_AllowingCaller_ToAdvanceBuffer()
    {
        var payload1 = Encoding.UTF8.GetBytes("{\"LL\":{}}");
        var payload2 = Encoding.UTF8.GetBytes("{\"LL\":{\"Code\":\"200\"}}");

        var msg1 = BuildMessage(LoxoneMessageType.Text, payload1);
        var msg2 = BuildMessage(LoxoneMessageType.Text, payload2);

        // Concatenate both messages in one buffer (simulates partial TCP receives)
        var combined = new byte[msg1.Length + msg2.Length];
        msg1.CopyTo(combined, 0);
        msg2.CopyTo(combined, msg1.Length);

        BinaryProtocolParser.TryParseMessage(combined, out var first, out var firstLen);
        Assert.Equal(msg1.Length, firstLen);
        Assert.Equal("{\"LL\":{}}", first!.Text);

        // Advance and parse second
        var remaining = new byte[combined.Length - firstLen];
        Array.Copy(combined, firstLen, remaining, 0, remaining.Length);
        BinaryProtocolParser.TryParseMessage(remaining, out var second, out _);
        Assert.Equal("{\"LL\":{\"Code\":\"200\"}}", second!.Text);
    }

    // ── BuildStateJson ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildStateJson_ProducesExpectedShape()
    {
        var json = BinaryProtocolParser.BuildStateJson("aabb-ccdd", "1.5");
        Assert.Equal("{\"uuid\":\"aabb-ccdd\",\"value\":\"1.5\"}", json);
    }

    // ── Keepalive ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseMessage_Keepalive_ParsesSuccessfully()
    {
        var data = BuildMessage(LoxoneMessageType.Keepalive, Array.Empty<byte>());
        var ok = BinaryProtocolParser.TryParseMessage(data, out var msg, out _);

        Assert.True(ok);
        Assert.NotNull(msg);
        Assert.Equal(LoxoneMessageType.Keepalive, msg!.MessageType);
        Assert.Null(msg.Text);
        Assert.Empty(msg.StateEvents);
    }
}
