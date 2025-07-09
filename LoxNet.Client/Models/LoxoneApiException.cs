using System;

namespace LoxNet;

/// <summary>
/// Exception thrown when the Miniserver returns a non-successful response code.
/// </summary>
public class LoxoneApiException : Exception
{
    /// <summary>The numeric response code.</summary>
    public int Code { get; }

    /// <summary>Creates the exception.</summary>
    public LoxoneApiException(int code, string? message)
        : base($"Server returned code {code}: {message}")
    {
        Code = code;
    }
}

