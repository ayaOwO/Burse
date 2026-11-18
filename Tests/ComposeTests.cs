using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tests;

[Category("Compose")]
public class ComposeTests
{
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Connect()
    {
        var url = Environment.GetEnvironmentVariable("BURSE_COMPOSE_URL");
        if (string.IsNullOrWhiteSpace(url))
            Assert.Ignore("Set BURSE_COMPOSE_URL to run these checks against a running Compose stack.");

        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(url!),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    [OneTimeTearDown]
    public void Disconnect() => _client?.Dispose();

    [TestCase("/")]
    [TestCase("/market/1")]
    public async Task ServesAngularPageAndBundle(string path)
    {
        using var page = await _client.GetAsync(path);
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(page.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));

        var html = await page.Content.ReadAsStringAsync();
        var baseHref = Regex.Match(html, """<base\b[^>]*\bhref="([^"]+)["]""", RegexOptions.IgnoreCase);
        var bundle = Regex.Match(html, """<script\b[^>]*\bsrc="([^"]+\.js)["]""", RegexOptions.IgnoreCase);
        Assert.That(bundle.Success, Is.True, "The Angular page should reference a JavaScript bundle.");

        var pageUri = page.RequestMessage!.RequestUri!;
        var baseUri = baseHref.Success ? new Uri(pageUri, baseHref.Groups[1].Value) : pageUri;
        using var script = await _client.GetAsync(new Uri(baseUri, bundle.Groups[1].Value));
        Assert.That(script.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(script.Content.Headers.ContentType?.MediaType, Does.Contain("javascript"));
    }

    [Test]
    public async Task ProxiesStockReadsAsJson()
    {
        using var response = await _client.GetAsync("/api/stocks/1");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        var stock = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stock");
        Assert.That(stock.GetProperty("id").GetInt32(), Is.EqualTo(1));
        Assert.That(stock.GetProperty("name").GetString(), Is.EqualTo("Apple"));
    }

    [Test]
    public async Task ProxiesOrderValidationAsProblemDetails()
    {
        using var payload = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/orders", payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo(400));
    }
}
