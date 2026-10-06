using IdentityService.Application.Features.Identity.Models;
using IdentityService.Application.Features.Identity.Validators;

namespace IdentityService.Application.Tests.Identity;

public sealed class RequestValidatorTests
{
    private static readonly string ValidToken = new('t', 40);

    [Theory]
    [InlineData("09121234567", true)]
    [InlineData("09999999999", true)]
    [InlineData("", false)]
    [InlineData("9121234567", false)]
    [InlineData("0912123456", false)]
    [InlineData("091212345678", false)]
    [InlineData("08121234567", false)]
    [InlineData("09a21234567", false)]
    public void SendCode_requires_an_Iranian_mobile_number(string phone, bool valid)
    {
        var result = new SendCodeRequestValidator().Validate(new SendCodeRequest(phone));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void SendCode_reports_the_format_message_on_PhoneNumber()
    {
        var result = new SendCodeRequestValidator().Validate(new SendCodeRequest("123"));

        var error = Assert.Single(result.Errors, e => e.PropertyName == "PhoneNumber" && e.ErrorMessage.Contains("09xxxxxxxxx"));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("09121234567", "12345", true)]
    [InlineData("09121234567", "1234", false)]
    [InlineData("09121234567", "123456", false)]
    [InlineData("09121234567", "12a45", false)]
    [InlineData("09121234567", "", false)]
    [InlineData("0912", "12345", false)]
    public void VerifyCode_requires_phone_and_a_five_digit_numeric_code(string phone, string code, bool valid)
    {
        var result = new VerifyCodeRequestValidator().Validate(new VerifyCodeRequest(phone, code));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(512, true)]
    [InlineData(31, false)]
    [InlineData(513, false)]
    [InlineData(0, false)]
    public void Refresh_and_logout_tokens_must_be_between_32_and_512_characters(int length, bool valid)
    {
        var token = new string('x', length);

        Assert.Equal(valid, new RefreshRequestValidator().Validate(new RefreshRequest(token)).IsValid);
        Assert.Equal(valid, new LogoutRequestValidator().Validate(new LogoutRequest(token)).IsValid);
    }

    [Fact]
    public void A_normal_token_passes_both_validators()
    {
        Assert.True(new RefreshRequestValidator().Validate(new RefreshRequest(ValidToken)).IsValid);
        Assert.True(new LogoutRequestValidator().Validate(new LogoutRequest(ValidToken)).IsValid);
    }
}
