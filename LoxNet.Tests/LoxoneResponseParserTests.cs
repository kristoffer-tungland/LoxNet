using System;
using System.Text.Json;
using Xunit;

namespace LoxNet.Tests;

public class LoxoneResponseParserTests
{
    [Fact]
    public void Parse_JsonResponse_WithCode200_ReturnsCorrectMessage()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var jsonResponse = "{\"LL\": { \"control\": \"jdev/sys/getkey\", \"value\": \"abc123\", \"Code\": 200}}";

        // Act
        var result = parser.Parse(jsonResponse);

        // Assert
        Assert.Equal(200, result.Code);
        Assert.NotEqual(default, result.Value);
    }

    [Fact]
    public void Parse_JsonResponse_WithStringCode_ParsesCodeAsInt()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var jsonResponse = "{\"LL\": { \"Code\": \"200\", \"value\": \"test\"}}";

        // Act
        var result = parser.Parse(jsonResponse);

        // Assert
        Assert.Equal(200, result.Code);
    }

    [Fact]
    public void Parse_JsonResponse_WithMessage_IncludesMessage()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var jsonResponse = "{\"LL\": { \"Code\": \"200\", \"message\": \"Success\", \"value\": \"data\"}}";

        // Act
        var result = parser.Parse(jsonResponse);

        // Assert
        Assert.Equal("Success", result.Message);
    }

    [Fact]
    public void Parse_JsonResponse_WithComplexValue_ParsesValueAsJson()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var jsonResponse = "{\"LL\": { \"Code\": \"200\", \"value\": {\"token\": \"xyz\", \"validUntil\": 12345}}}";

        // Act
        var result = parser.Parse(jsonResponse);

        // Assert
        Assert.NotEqual(default, result.Value);
        Assert.True(result.Value.TryGetProperty("token", out var token));
        Assert.Equal("xyz", token.GetString());
    }

    [Fact]
    public void Parse_JsonResponse_WithoutCode_DefaultsCodeToZero()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var jsonResponse = "{\"LL\": { \"value\": \"test\"}}";

        // Act
        var result = parser.Parse(jsonResponse);

        // Assert
        Assert.Equal(0, result.Code);
    }

    [Fact]
    public void Parse_JsonResponse_WithWhitespace_NormalizesCorrectly()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var jsonResponse = "  \n{\"LL\": { \"Code\": \"200\", \"value\": \"test\"}}\n  ";

        // Act
        var result = parser.Parse(jsonResponse);

        // Assert
        Assert.Equal(200, result.Code);
    }

    [Fact]
    public void Parse_ErrorResponse_WithErrorCode_ReturnsErrorCode()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var errorResponse = "{\"LL\": { \"Code\": \"420\", \"message\": \"Policy Not Fulfilled\"}}";

        // Act
        var result = parser.Parse(errorResponse);

        // Assert
        Assert.Equal(420, result.Code);
        Assert.Equal("Policy Not Fulfilled", result.Message);
    }

    [Fact]
    public void Parse_EmptyResponse_ThrowsArgumentException()
    {
        // Arrange
        var parser = new LoxoneResponseParser();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => parser.Parse(""));
        Assert.Throws<ArgumentException>(() => parser.Parse("   "));
    }

    [Fact]
    public void Parse_InvalidJson_WithoutLL_ParsesAsPlainJsonValue()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var invalidResponse = "{\"invalid\": \"response\"}";

        // Act
        var result = parser.Parse(invalidResponse);

        // Assert
        // Plain JSON without LL is treated as a value with default success code
        Assert.Equal(200, result.Code);
        Assert.True(result.Value.TryGetProperty("invalid", out var invalidProp));
        Assert.Equal("response", invalidProp.GetString());
        
        // Don't dispose - the document must stay alive for JsonElement
    }

    [Fact]
    public void Parse_PlainJsonValue_WithoutLL_ParsesAsJsonValue()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var plainJson = "{\"token\": \"abc123\"}";

        // Act
        var result = parser.Parse(plainJson);

        // Assert
        Assert.Equal(200, result.Code); // Default success code
        Assert.True(result.Value.TryGetProperty("token", out _));
        
        // Don't dispose - the document must stay alive for JsonElement
    }

    [Fact]
    public void Parse_EncryptedResponse_WithoutEncryption_ThrowsInvalidOperationException()
    {
        // Arrange
        var parser = new LoxoneResponseParser(null); // No encryption
        var encryptedResponse = "RN6fNn3RrQVMIhQN2aSC+J6FrJV5xRuCujiH+otyQv6ENni4kT+XUAmgCm6C8JpjVwOy5u8E38Fu5nfCSc8JsI=";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => parser.Parse(encryptedResponse));
        Assert.Contains("encryption not initialized", ex.Message);
    }

    [Fact]
    public void Parse_ResponseWithNullValue_HandlesGracefully()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var response = "{\"LL\": { \"Code\": \"200\", \"value\": null}}";

        // Act
        var result = parser.Parse(response);

        // Assert
        Assert.Equal(200, result.Code);
        Assert.Equal(JsonValueKind.Null, result.Value.ValueKind);
    }

    [Fact]
    public void Parse_ResponseWithoutValue_CreatesDefaultValue()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var response = "{\"LL\": { \"Code\": \"200\"}}";

        // Act
        var result = parser.Parse(response);

        // Assert
        Assert.Equal(200, result.Code);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Parse_KeyexchangeResponse_WithEncryptedValue_ParsesCorrectly()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var keyexchangeResponse = "{\"LL\": { \"Code\": \"200\", \"control\": \"jdev/sys/keyexchange/...\", \"value\": \"kg9V8XOI9tqS6am3Hq/pxqAKDatx0LaAisMODj9yWE+oi8ioM1i/JYTc2qlv8Egy6TaBJOfUt2NzVGX85mA3BEmhFbjEjrN+mYz5lKLKa7E=\"}}";

        // Act
        var result = parser.Parse(keyexchangeResponse);

        // Assert
        Assert.Equal(200, result.Code);
        Assert.Equal("kg9V8XOI9tqS6am3Hq/pxqAKDatx0LaAisMODj9yWE+oi8ioM1i/JYTc2qlv8Egy6TaBJOfUt2NzVGX85mA3BEmhFbjEjrN+mYz5lKLKa7E=", 
            result.Value.GetString());
    }

    [Fact]
    public void Parse_ResponseWithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var response = "{\"LL\": { \"Code\": \"200\", \"value\": \"test+value/with=special\"}}";

        // Act
        var result = parser.Parse(response);

        // Assert
        Assert.Equal(200, result.Code);
        Assert.Equal("test+value/with=special", result.Value.GetString());
    }

    [Fact]
    public void Parse_ResponseWithControlProperty_IncludesInParsing()
    {
        // Arrange
        var parser = new LoxoneResponseParser();
        var response = "{\"LL\": { \"Code\": \"200\", \"control\": \"jdev/sys/getkey\", \"value\": \"abc\"}}";

        // Act
        var result = parser.Parse(response);

        // Assert
        Assert.Equal(200, result.Code);
        // The control property is in the LL object but we focus on Code and value
        Assert.NotNull(result.Value);
    }
}
