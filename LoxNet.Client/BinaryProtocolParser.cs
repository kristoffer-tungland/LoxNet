using System;
using System.Text;

namespace LoxNet;

/// <summary>
/// Parses Loxone binary protocol messages.
/// Format: [4 bytes: message type/flags] [4 bytes: payload length] [N bytes: JSON payload]
/// </summary>
public static class BinaryProtocolParser
{
    private const int HeaderSize = 8;

    /// <summary>
    /// Parses a binary protocol message and extracts the JSON payload.
    /// </summary>
    /// <param name="data">Raw binary data received from WebSocket.</param>
    /// <returns>The JSON payload as a string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when data is incomplete or malformed.</exception>
    public static string ParseMessage(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < HeaderSize)
            throw new InvalidOperationException($"Incomplete message header: expected {HeaderSize} bytes, got {data.Length}.");

        // Parse header: 4 bytes message type/flags + 4 bytes payload length (little-endian)
        uint payloadLength = BitConverter.ToUInt32(data, 4);

        if (data.Length < HeaderSize + payloadLength)
            throw new InvalidOperationException($"Incomplete payload: expected {payloadLength} bytes, got {data.Length - HeaderSize}.");

        // Extract and decode JSON payload
        var json = Encoding.UTF8.GetString(data, HeaderSize, (int)payloadLength);
        return json;
    }

    /// <summary>
    /// Tries to parse a binary protocol message and extract the JSON payload.
    /// </summary>
    /// <param name="data">Raw binary data received from WebSocket.</param>
    /// <param name="json">The extracted JSON payload, if successful.</param>
    /// <returns>True if parsing succeeded; false if the message is incomplete or malformed.</returns>
    public static bool TryParseMessage(byte[] data, out string json, out int totalMessageLength)
    {
        json = string.Empty;
        totalMessageLength = 0;

        if (data == null || data.Length < HeaderSize)
            return false;

        try
        {
            uint payloadLength = BitConverter.ToUInt32(data, 4);
            var required = HeaderSize + (int)payloadLength;

            if (data.Length < required)
                return false;

            json = Encoding.UTF8.GetString(data, HeaderSize, (int)payloadLength);
            totalMessageLength = required;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
