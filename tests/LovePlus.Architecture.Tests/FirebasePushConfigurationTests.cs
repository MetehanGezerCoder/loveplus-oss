using System.Text.Json;
using LovePlus.Infrastructure.Notifications;

namespace LovePlus.Architecture.Tests;

public sealed class FirebasePushConfigurationTests
{
    [Fact]
    public void An_unconfigured_firebase_section_reports_itself_as_unconfigured()
    {
        Assert.False(new FirebasePushOptions().IsConfigured());
        Assert.Null(new FirebasePushOptions().ReadServiceAccountJson());
    }

    [Fact]
    public void A_missing_service_account_file_does_not_count_as_configuration()
    {
        var options = new FirebasePushOptions
        {
            ServiceAccountJsonPath = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json")
        };

        Assert.False(options.IsConfigured());
    }

    [Fact]
    public void Inline_json_is_preferred_over_a_path()
    {
        var options = new FirebasePushOptions {ServiceAccountJson = "{\"project_id\":\"x\"}"};

        Assert.True(options.IsConfigured());
        Assert.Equal("{\"project_id\":\"x\"}", options.ReadServiceAccountJson());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"project_id\":\"loveplus\"}")]
    [InlineData("{\"project_id\":\"loveplus\",\"client_email\":\"a@b.iam.gserviceaccount.com\"}")]
    public void An_incomplete_service_account_is_rejected_rather_than_half_used(string json)
    {
        // A half-parsed service account would fail later, inside a notification send, where the
        // only visible symptom is a missing notification.
        Assert.Throws<InvalidOperationException>(() => GoogleServiceAccount.Parse(json));
    }

    [Fact]
    public void Malformed_service_account_json_is_rejected()
    {
        Assert.Throws<JsonException>(() => GoogleServiceAccount.Parse("not json"));
    }

    [Fact]
    public void A_complete_service_account_defaults_the_google_token_endpoint()
    {
        var account = GoogleServiceAccount.Parse(
            """
            {
              "project_id": "loveplus",
              "client_email": "push@loveplus.iam.gserviceaccount.com",
              "private_key": "-----BEGIN PRIVATE KEY-----\nnot-a-real-key\n-----END PRIVATE KEY-----\n"
            }
            """);

        Assert.Equal("loveplus", account.ProjectId);
        Assert.Equal("https://oauth2.googleapis.com/token", account.ResolvedTokenUri);
    }
}
