using System;
using Flow.Core.Language;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Developer;

public class GoldenCasingTests
{
    [Theory]
    [InlineData("get user profile", "getUserProfile")]
    [InlineData("HTTP response", "httpResponse")]
    [InlineData("API client", "apiClient")]
    [InlineData("API client v2", "apiClientV2")]
    [InlineData("HTTP 2 client", "http2Client")]
    [InlineData("OAuth 2 token", "oauth2Token")]
    [InlineData("JSON parser", "jsonParser")]
    [InlineData("XML HTTP request", "xmlHttpRequest")]
    [InlineData("user profile service", "userProfileService")]
    [InlineData("database repository", "databaseRepository")]
    [InlineData("maximum retry count", "maximumRetryCount")]
    [InlineData("point 2 D", "point2D")]
    [InlineData("IPv6 parser", "ipv6Parser")]
    [InlineData("H264 decoder", "h264Decoder")]
    public void Golden_ToCamelCase_MatchesExpectedOutput(string input, string expected)
    {
        string actual = CasingTransformer.ToCamelCase(input);
        Assert.Equal(expected, actual);
        // Also assert idempotence
        Assert.Equal(expected, CasingTransformer.ToCamelCase(actual));
    }

    [Theory]
    [InlineData("get user profile", "GetUserProfile")]
    [InlineData("HTTP response", "HttpResponse")]
    [InlineData("API client", "ApiClient")]
    [InlineData("API client v2", "ApiClientV2")]
    [InlineData("HTTP 2 client", "Http2Client")]
    [InlineData("OAuth 2 token", "OAuth2Token")]
    [InlineData("JSON parser", "JsonParser")]
    [InlineData("XML HTTP request", "XmlHttpRequest")]
    [InlineData("user profile service", "UserProfileService")]
    [InlineData("database repository", "DatabaseRepository")]
    [InlineData("maximum retry count", "MaximumRetryCount")]
    [InlineData("point 2 D", "Point2D")]
    [InlineData("IPv6 parser", "IPv6Parser")]
    [InlineData("H264 decoder", "H264Decoder")]
    public void Golden_ToPascalCase_MatchesExpectedOutput(string input, string expected)
    {
        string actual = CasingTransformer.ToPascalCase(input);
        Assert.Equal(expected, actual);
        // Also assert idempotence
        Assert.Equal(expected, CasingTransformer.ToPascalCase(actual));
    }

    [Theory]
    [InlineData("get user profile", "get_user_profile")]
    [InlineData("HTTP response", "http_response")]
    [InlineData("API client", "api_client")]
    [InlineData("API client v2", "api_client_v2")]
    [InlineData("HTTP 2 client", "http_2_client")]
    [InlineData("OAuth 2 token", "oauth_2_token")]
    [InlineData("JSON parser", "json_parser")]
    [InlineData("XML HTTP request", "xml_http_request")]
    [InlineData("user profile service", "user_profile_service")]
    [InlineData("database repository", "database_repository")]
    [InlineData("maximum retry count", "maximum_retry_count")]
    [InlineData("point 2 D", "point_2_d")]
    [InlineData("IPv6 parser", "ipv6_parser")]
    [InlineData("H264 decoder", "h264_decoder")]
    public void Golden_ToSnakeCase_MatchesExpectedOutput(string input, string expected)
    {
        string actual = CasingTransformer.ToSnakeCase(input);
        Assert.Equal(expected, actual);
        // Also assert idempotence
        Assert.Equal(expected, CasingTransformer.ToSnakeCase(actual));
    }

    [Theory]
    [InlineData("get user profile", "GET_USER_PROFILE")]
    [InlineData("HTTP response", "HTTP_RESPONSE")]
    [InlineData("API client", "API_CLIENT")]
    [InlineData("API client v2", "API_CLIENT_V2")]
    [InlineData("HTTP 2 client", "HTTP_2_CLIENT")]
    [InlineData("OAuth 2 token", "OAUTH_2_TOKEN")]
    [InlineData("JSON parser", "JSON_PARSER")]
    [InlineData("XML HTTP request", "XML_HTTP_REQUEST")]
    [InlineData("user profile service", "USER_PROFILE_SERVICE")]
    [InlineData("database repository", "DATABASE_REPOSITORY")]
    [InlineData("maximum retry count", "MAXIMUM_RETRY_COUNT")]
    [InlineData("point 2 D", "POINT_2_D")]
    [InlineData("IPv6 parser", "IPV6_PARSER")]
    [InlineData("H264 decoder", "H264_DECODER")]
    public void Golden_ToConstantCase_MatchesExpectedOutput(string input, string expected)
    {
        string actual = CasingTransformer.ToConstantCase(input);
        Assert.Equal(expected, actual);
        // Also assert idempotence
        Assert.Equal(expected, CasingTransformer.ToConstantCase(actual));
    }

    [Theory]
    [InlineData("get user profile", "get-user-profile")]
    [InlineData("HTTP response", "http-response")]
    [InlineData("API client", "api-client")]
    [InlineData("API client v2", "api-client-v2")]
    [InlineData("HTTP 2 client", "http-2-client")]
    [InlineData("OAuth 2 token", "oauth-2-token")]
    [InlineData("JSON parser", "json-parser")]
    [InlineData("XML HTTP request", "xml-http-request")]
    [InlineData("user profile service", "user-profile-service")]
    [InlineData("database repository", "database-repository")]
    [InlineData("maximum retry count", "maximum-retry-count")]
    [InlineData("point 2 D", "point-2-d")]
    [InlineData("IPv6 parser", "ipv6-parser")]
    [InlineData("H264 decoder", "h264-decoder")]
    public void Golden_ToKebabCase_MatchesExpectedOutput(string input, string expected)
    {
        string actual = CasingTransformer.ToKebabCase(input);
        Assert.Equal(expected, actual);
        // Also assert idempotence
        Assert.Equal(expected, CasingTransformer.ToKebabCase(actual));
    }

    [Theory]
    [InlineData("APIClientV2")]
    [InlineData("HTTP2Client")]
    [InlineData("OAuth2Token")]
    [InlineData("IPv6Parser")]
    [InlineData("JSONParser")]
    [InlineData("XMLHttpRequest")]
    [InlineData("HTTPRequestHandler")]
    [InlineData("V2Endpoint")]
    [InlineData("Point2D")]
    [InlineData("H264Decoder")]
    [InlineData("UTF8Parser")]
    [InlineData("getUserProfile")]
    [InlineData("UserProfileService")]
    [InlineData("IUserRepository")]
    [InlineData("MAX_RETRY_COUNT")]
    [InlineData("user_profile_service")]
    [InlineData("user-auth-service")]
    public void Preserves_Existing_Identifiers_WithoutExplicitCasing(string existingIdentifier)
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = $"Please inspect {existingIdentifier} in the codebase.";
        string formatted = pipeline.Format(input);

        Assert.Contains(existingIdentifier, formatted);
    }
}
