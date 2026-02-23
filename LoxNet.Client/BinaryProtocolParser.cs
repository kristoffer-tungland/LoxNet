using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace LoxNet;

/// <summary>
/// Message types as defined in the Loxone binary WebSocket protocol.
/// The identifier byte is the 2nd byte of the 8-byte message header.
/// </summary>
public enum LoxoneMessageType : byte
{
    /// <summary>Text (JSON) response to a command.</summary>
    Text = 0x00,
    /// <summary>Binary file (e.g. structure file).</summary>
    BinaryFile = 0x01,
    /// <summary>Value-state event table: UUID (16 bytes) + double (8 bytes) pairs.</summary>
    ValueStates = 0x02,
    /// <summary>Text-state event table.</summary>
    TextStates = 0x03,
    /// <summary>Daytimer-state event table.</summary>
    DaytimerStates = 0x04,
    /// <summary>Out-of-service indicator.</summary>
    OutOfService = 0x05,
    /// <summary>Keepalive response.</summary>
    Keepalive = 0x06,
    /// <summary>Weather-state event table.</summary>
    WeatherStates = 0x07,
}

/// <summary>
/// A single fully-parsed Loxone binary protocol message.
/// </summary>
public sealed class LoxoneBinaryMessage
{
    /// <summary>The message type decoded from the header identifier byte.</summary>
    public LoxoneMessageType MessageType { get; }

    /// <summary>
    /// For <see cref="LoxoneMessageType.Text"/> messages this contains the JSON string.
    /// For binary event-table messages this is <c>null</c>; use <see cref="StateEvents"/> instead.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// State events decoded from a Value-States or Text-States event table.
    /// Each entry maps a control UUID (formatted as Loxone uses: "aabbccdd-eeff-...") to its new string value.
    /// Empty for non-event-table messages.
    /// </summary>
    public IReadOnlyList<(string Uuid, string Value)> StateEvents { get; }

    /// <summary>
    /// The raw payload bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    internal LoxoneBinaryMessage(LoxoneMessageType type, string? text, IReadOnlyList<(string, string)> events, ReadOnlyMemory<byte> payload)
    {
        MessageType = type;
        Text = text;
        StateEvents = events;
        Payload = payload;
    }
}

/// <summary>
/// Parses the Loxone binary WebSocket protocol.
///
/// Each message from the Miniserver is preceded by an 8-byte header:
/// <code>
///   Byte 0    : 0x03  (magic / binary type marker)
///   Byte 1    : identifier (LoxoneMessageType)
///   Byte 2    : info flags (bit 7 = length is estimated)
///   Byte 3    : reserved
///   Bytes 4-7 : payload length, 32-bit unsigned little-endian
/// </code>
/// See: Communicating with the Miniserver – "Message Header" section.
/// </summary>
public static class BinaryProtocolParser
{
    private const int HeaderSize = 8;
    private const byte Magic = 0x03;

    // Size of a single value-state record: 16-byte UUID + 8-byte double = 24 bytes
    private const int ValueStateRecordSize = 16 + 8;

    /// <summary>
    /// Attempts to parse one complete Loxone binary protocol message from the front of
    /// <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Raw binary buffer (may contain more data after this message).</param>
    /// <param name="message">The parsed message if the method returns <c>true</c>.</param>
    /// <param name="totalMessageLength">
    /// Total number of bytes consumed (header + payload) so the caller can advance the buffer.
    /// </param>
    /// <returns>
    /// <c>true</c> when a complete message was successfully parsed; <c>false</c> when the buffer
    /// holds fewer bytes than the indicated payload length (i.e. more data is still in transit).
    /// </returns>
    public static bool TryParseMessage(byte[] data, out LoxoneBinaryMessage? message, out int totalMessageLength)
    {
        message = null;
        totalMessageLength = 0;

        if (data == null || data.Length < HeaderSize)
            return false;

        // Byte 0 must be the magic marker 0x03
        if (data[0] != Magic)
            return false;

        var msgType = (LoxoneMessageType)data[1];
        // Byte 2 bit-7: payload length is only estimated (real length follows separately)
        bool estimated = (data[2] & 0x80) != 0;
        uint payloadLength = BitConverter.ToUInt32(data, 4);

        if (estimated)
        {
            // Estimated-length messages: we wait for the real header that follows the payload.
            // For safety we still need the payload in the buffer; treat as incomplete.
        }

        int required = HeaderSize + (int)payloadLength;
        if (data.Length < required)
            return false;

        try
        {
            var payload = new ReadOnlyMemory<byte>(data, HeaderSize, (int)payloadLength);
            message = ParsePayload(msgType, payload);
            totalMessageLength = required;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Internal helpers ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a payload frame that has already been matched to a header.
    /// Called by <see cref="LoxoneWebSocketClient"/> after the two-frame handshake completes.
    /// </summary>
    internal static LoxoneBinaryMessage ParsePayloadPublic(LoxoneMessageType type, ReadOnlyMemory<byte> payload)
        => ParsePayload(type, payload);

    private static LoxoneBinaryMessage ParsePayload(LoxoneMessageType type, ReadOnlyMemory<byte> payload)
    {
        switch (type)
        {
            case LoxoneMessageType.Text:
            {
                var text = Encoding.UTF8.GetString(payload.Span);
                return new LoxoneBinaryMessage(type, text, Array.Empty<(string, string)>(), payload);
            }

            case LoxoneMessageType.ValueStates:
            {
                var events = ParseValueStatesTable(payload.Span);
                return new LoxoneBinaryMessage(type, null, events, payload);
            }

            case LoxoneMessageType.TextStates:
            {
                var events = ParseTextStatesTable(payload.ToArray());
                return new LoxoneBinaryMessage(type, null, events, payload);
            }

            case LoxoneMessageType.Keepalive:
                return new LoxoneBinaryMessage(type, null, Array.Empty<(string, string)>(), payload);

            default:
                // BinaryFile, DaytimerStates, OutOfService, WeatherStates – return raw for now
                return new LoxoneBinaryMessage(type, null, Array.Empty<(string, string)>(), payload);
        }
    }

    /// <summary>
    /// Parses a Value-States event table.
    /// Each record is 24 bytes: 16-byte UUID (little-endian) + 8-byte IEEE 754 double.
    /// </summary>
    private static IReadOnlyList<(string Uuid, string Value)> ParseValueStatesTable(ReadOnlySpan<byte> data)
    {
        int count = data.Length / ValueStateRecordSize;
        if (count == 0)
            return Array.Empty<(string, string)>();

        var list = new List<(string, string)>(count);

        for (int i = 0; i < count; i++)
        {
            int offset = i * ValueStateRecordSize;
            var uuidBytes = data.Slice(offset, 16);
            var uuidStr = LoxoneUuidToString(uuidBytes);
            double value = BitConverter.ToDouble(data.Slice(offset + 16, 8).ToArray(), 0);
            list.Add((uuidStr, FormatDouble(value)));
        }

        return list;
    }

    /// <summary>
    /// Parses a Text-States event table.
    /// Each record (padded to 4-byte boundary):
    ///   16 bytes  uuid
    ///   16 bytes  icon uuid (ignored)
    ///    4 bytes  text length (uint32 LE)
    ///   N bytes   UTF-8 text
    ///   padding   to next 4-byte boundary
    /// </summary>
    private static IReadOnlyList<(string Uuid, string Value)> ParseTextStatesTable(byte[] data)
    {
        var list = new List<(string, string)>();
        int pos = 0;

        while (pos + 36 <= data.Length) // 16 + 16 + 4 minimum
        {
            var uuidStr = LoxoneUuidToString(new ReadOnlySpan<byte>(data, pos, 16));
            pos += 32; // skip uuid (16) + icon uuid (16)

            if (pos + 4 > data.Length)
                break;

            uint textLen = BitConverter.ToUInt32(data, pos);
            pos += 4;

            if (pos + (int)textLen > data.Length)
                break;

            var text = Encoding.UTF8.GetString(data, pos, (int)textLen);
            list.Add((uuidStr, text));

            // Advance past text, padded up to next 4-byte boundary
            int recordDataLen = 4 + (int)textLen;
            int padded = ((16 + 16 + recordDataLen - 1) / 4 + 1) * 4;
            // pos is already at start-of-text; advance by padded minus the fixed header we already consumed
            int advance = padded - 32 - 4; // padded total - uuid - iconuuid - textlen
            pos += advance;
        }

        return list;
    }

    /// <summary>
    /// Converts a 16-byte little-endian UUID (as used by Loxone) to the Loxone string format:
    /// <c>aabbccdd-eeff-gghh-iijjkkll</c> (no 5th group, first 4 groups separated by dashes).
    /// </summary>
    private static string LoxoneUuidToString(ReadOnlySpan<byte> b)
    {
        // Loxone UUID layout (little-endian .NET Guid):
        //   bytes 0-3   → Data1 (uint32 LE)
        //   bytes 4-5   → Data2 (uint16 LE)
        //   bytes 6-7   → Data3 (uint16 LE)
        //   bytes 8-15  → Data4 (big-endian / as-is)
        //
        // The URN from System.Guid gives "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" (5 groups),
        // but Loxone uses 4 groups where the last two standard groups are concatenated:
        //   "aabbccdd-eeff-gghh-iijjkkllmmnnoopp"
        var guid = new Guid(b.ToArray());
        var urn = guid.ToString("D"); // xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        // Re-format: merge the last two groups (groups[3] + groups[4]) into one
        var parts = urn.Split('-');
        return $"{parts[0]}-{parts[1]}-{parts[2]}-{parts[3]}{parts[4]}";
    }

    /// <summary>Formats a double value the same way the Miniserver serialises state values.</summary>
    private static string FormatDouble(double value)
    {
        // Use the round-trip format so we don't lose precision; consumers can parse as needed.
        return value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string BuildStateJson(string uuid, string value)
    {
        // Produce {"uuid":"…","value":"…"} — same shape consumed by HandleWebSocketMessage
        using var ms = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartObject();
        writer.WriteString("uuid", uuid);
        writer.WriteString("value", value);
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
