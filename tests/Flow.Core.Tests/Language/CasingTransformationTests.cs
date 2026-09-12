using Flow.Core.Language;
using Xunit;

namespace Flow.Core.Tests.Language;

public class CasingTransformationTests
{
    [Theory]
    [InlineData("get user name", "getUserName")]
    [InlineData("user profile manager", "userProfileManager")]
    [InlineData("utf 8 encoder", "utf8Encoder")]
    [InlineData("is valid email address", "isValidEmailAddress")]
    [InlineData("api key", "apiKey")]
    [InlineData("single", "single")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ToCamelCase_TransformsCorrectly(string input, string expected)
    {
        string actual = CasingTransformer.ToCamelCase(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("user profile manager", "UserProfileManager")]
    [InlineData("get user name", "GetUserName")]
    [InlineData("http client helper", "HttpClientHelper")]
    [InlineData("single", "Single")]
    [InlineData("", "")]
    public void ToPascalCase_TransformsCorrectly(string input, string expected)
    {
        string actual = CasingTransformer.ToPascalCase(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("get user name", "get_user_name")]
    [InlineData("user profile manager", "user_profile_manager")]
    [InlineData("max retry count", "max_retry_count")]
    [InlineData("single", "single")]
    [InlineData("", "")]
    public void ToSnakeCase_TransformsCorrectly(string input, string expected)
    {
        string actual = CasingTransformer.ToSnakeCase(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("user profile manager", "user-profile-manager")]
    [InlineData("get user name", "get-user-name")]
    [InlineData("primary button component", "primary-button-component")]
    [InlineData("single", "single")]
    [InlineData("", "")]
    public void ToKebabCase_TransformsCorrectly(string input, string expected)
    {
        string actual = CasingTransformer.ToKebabCase(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("max retry count", "MAX_RETRY_COUNT")]
    [InlineData("default timeout ms", "DEFAULT_TIMEOUT_MS")]
    [InlineData("api secret key", "API_SECRET_KEY")]
    [InlineData("single", "SINGLE")]
    [InlineData("", "")]
    public void ToConstantCase_TransformsCorrectly(string input, string expected)
    {
        string actual = CasingTransformer.ToConstantCase(input);
        Assert.Equal(expected, actual);
    }
}
