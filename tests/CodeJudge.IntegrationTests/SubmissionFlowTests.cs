using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CodeJudge.IntegrationTests;

public sealed class SubmissionFlowTests : IClassFixture<CodeJudgeWebApplicationFactory>
{
    private readonly CodeJudgeWebApplicationFactory _factory;

    public SubmissionFlowTests(CodeJudgeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", CodeJudgeWebApplicationFactory.ApiKey);
        return client;
    }

    [Fact]
    public async Task Post_without_key_returns_401_with_errorCode_4001()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/submissions", new
        {
            userId = "user-1",
            problemId = "sum-two-numbers",
            language = "Python",
            code = "def sum_two(a, b): return a + b",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeOf(response)).Should().Be(4001);
    }

    [Fact]
    public async Task Post_unknown_problem_returns_400_with_errorCode_3001()
    {
        var response = await AuthedClient().PostAsJsonAsync("/api/v1/submissions", new
        {
            userId = "user-1",
            problemId = "does-not-exist",
            language = "Python",
            code = "def sum_two(a, b): return a + b",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeOf(response)).Should().Be(3001);
    }

    [Fact]
    public async Task Post_unknown_language_returns_400_with_errorCode_3002()
    {
        // The enum converter rejects "cobol" during binding; that must surface as an ApiResult 400,
        // not the built-in model-state response and not a 500.
        var response = await AuthedClient().PostAsJsonAsync("/api/v1/submissions", new
        {
            userId = "user-1",
            problemId = "sum-two-numbers",
            language = "cobol",
            code = "x = 1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeOf(response)).Should().Be(3002);
    }

    [Fact]
    public async Task Get_unknown_submission_returns_404_with_errorCode_2001()
    {
        var response = await AuthedClient().GetAsync($"/api/v1/submissions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeOf(response)).Should().Be(2001);
    }

    [Fact]
    public async Task Created_Location_header_uses_documented_v1_lowercase_path()
    {
        var response = await AuthedClient().PostAsJsonAsync("/api/v1/submissions", new
        {
            userId = "user-1",
            problemId = "sum-two-numbers",
            language = "Python",
            code = "def sum_two(a, b): return a + b",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.PathAndQuery.Should().StartWith("/api/v1/submissions/");
    }

    [Fact]
    public async Task Post_then_Get_returns_pending_submission_in_ApiResult_shape()
    {
        var client = AuthedClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/submissions", new
        {
            userId = "user-1",
            problemId = "sum-two-numbers",
            language = "Python",
            code = "def sum_two(a, b): return a + b",
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var root = created.RootElement;
        root.GetProperty("status").GetBoolean().Should().BeTrue();

        var data = root.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("pending");
        data.GetProperty("results").GetArrayLength().Should().Be(0);

        var id = data.GetProperty("id").GetString();

        var getResponse = await client.GetAsync($"/api/v1/submissions/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var fetched = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        fetched.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("pending");
    }

    private static async Task<int> ErrorCodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("errorCode").GetInt32();
    }
}
