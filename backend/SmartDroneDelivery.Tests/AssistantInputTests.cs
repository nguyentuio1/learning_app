using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class AssistantInputTests
{
    [Theory]
    [InlineData("The recorded arrival estimate is 2026-11-16 20:30:00 UTC.")]
    [InlineData("Recent recorded events: 2026-11-16 20:31:00 UTC: arrived.")]
    public void Legitimate_grounded_dates_are_not_hidden_as_phone_numbers(string text)
        => Assert.Equal(text, AssistantInput.SafeHistory(text));

    [Fact]
    public void Excessive_legacy_history_is_hidden_without_publishing_an_unbounded_message()
        => Assert.Equal(AssistantInput.Hidden, AssistantInput.SafeHistory(new string('a', 4001)));

    [Theory]
    [InlineData("Has the recipient received the package?")]
    [InlineData("Người nhận đã nhận hàng chưa?")]
    public void Receipt_status_question_without_identity_or_contact_data_remains_supported(string text)
        => Assert.False(AssistantInput.IsSensitive(text));

    [Theory]
    [InlineData("0909999999")]
    [InlineData("+84 90 999 9999")]
    [InlineData("2026-11-16 phone 0909999999")]
    [InlineData("password=abc")]
    [InlineData("Bearer some-credential")]
    [InlineData("alice@example.com")]
    [InlineData("The recipient address is 12 Main Street; what is the status?")]
    [InlineData("Địa chỉ là 12 Lê Lợi, cho tôi biết trạng thái đơn")]
    [InlineData("Người nhận: Nguyễn Văn A")]
    [InlineData("Contact Alice at 12 Oak Road")]
    public void Secret_and_contact_patterns_are_hidden(string text) => Assert.True(AssistantInput.IsSensitive(text));
}
