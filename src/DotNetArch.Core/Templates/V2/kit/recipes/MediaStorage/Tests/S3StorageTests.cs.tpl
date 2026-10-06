using System.Net;
using System.Text;
using {{Prefix}}.Kit.MediaStorage.Core;
using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.MediaStorage.Tests;

public class S3StorageTests
{
    private const string Section = "MediaStorage:Test";

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> Valid() => new()
    {
        [Section + ":Endpoint"] = "http://storage:9000/",
        [Section + ":Bucket"] = "media",
        [Section + ":PublicBaseUrl"] = "https://cdn.example.com/media/"
    };

    private static S3StorageOptions Bind(Dictionary<string, string?> values) =>
        S3StorageOptionsBinder.Bind(Config(values), Section, "TEST_ACCESS_KEY", "TEST_SECRET_KEY");

    [Fact]
    public void Valid_options_are_normalised_and_anonymous_without_keys()
    {
        var options = Bind(Valid());

        Assert.Equal("http://storage:9000", options.Endpoint);
        Assert.Equal("media", options.Bucket);
        Assert.True(options.ForcePathStyle);
        Assert.True(options.IsAnonymous);
    }

    [Theory]
    [InlineData("Endpoint")]
    [InlineData("Bucket")]
    public void Required_options_are_reported_by_key(string key)
    {
        var values = Valid();
        values.Remove($"{Section}:{key}");

        Assert.Contains($"{Section}:{key}", Assert.Throws<InvalidOperationException>(() => Bind(values)).Message);
    }

    [Fact]
    public void Half_configured_credentials_are_rejected()
    {
        var values = Valid();
        values["TEST_ACCESS_KEY"] = "access";

        var error = Assert.Throws<InvalidOperationException>(() => Bind(values));

        Assert.Contains("TEST_SECRET_KEY", error.Message);
    }

    [Fact]
    public void Both_credentials_make_the_access_credentialed()
    {
        var values = Valid();
        values["TEST_ACCESS_KEY"] = "access";
        values["TEST_SECRET_KEY"] = "secret";

        Assert.False(Bind(values).IsAnonymous);
    }

    [Theory]
    [InlineData("ftp://storage")]
    [InlineData("not a url")]
    public void The_endpoint_must_be_an_http_url(string endpoint)
    {
        var values = Valid();
        values[Section + ":Endpoint"] = endpoint;

        Assert.Throws<InvalidOperationException>(() => Bind(values));
    }

    [Fact]
    public void Public_urls_are_built_from_the_public_base_and_escape_segments()
    {
        using var storage = new S3CompatibleMediaStorage("Test", Bind(Valid()), S3ClientFactory.Create(Bind(Valid()), new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        Assert.Equal("https://cdn.example.com/media/photos/my%20cat.jpg", storage.GetPublicUrl("/photos/my cat.jpg"));
    }

    [Fact]
    public void Public_urls_need_a_configured_public_base()
    {
        var values = Valid();
        values.Remove(Section + ":PublicBaseUrl");
        var options = Bind(values);
        using var storage = new S3CompatibleMediaStorage("Test", options, S3ClientFactory.Create(options, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        Assert.Throws<InvalidOperationException>(() => storage.GetPublicUrl("a.jpg"));
    }

    [Fact]
    public async Task Listing_reads_keys_from_the_store_response()
    {
        const string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">" +
            "<Name>media</Name><Prefix>p/</Prefix><KeyCount>2</KeyCount><MaxKeys>1000</MaxKeys><IsTruncated>false</IsTruncated>" +
            "<Contents><Key>p/a.jpg</Key><LastModified>2026-01-01T00:00:00.000Z</LastModified><ETag>\"a\"</ETag><Size>10</Size><StorageClass>STANDARD</StorageClass></Contents>" +
            "<Contents><Key>p/b.jpg</Key><LastModified>2026-01-02T00:00:00.000Z</LastModified><ETag>\"b\"</ETag><Size>20</Size><StorageClass>STANDARD</StorageClass></Contents>" +
            "</ListBucketResult>";
        var options = Bind(Valid());
        using var storage = new S3CompatibleMediaStorage("Test", options, S3ClientFactory.Create(options, new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") })));

        var keys = await storage.ListKeysAsync("p/");

        Assert.Equal(new[] { "p/a.jpg", "p/b.jpg" }, keys);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
