using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class GeminiModelHandlerTests
{
    private static object Generation => new { model = "gemini-3.5-flash-lite", stream = false,
        system = "Classify a synthetic question.", prompt = "Where is the demo parcel?",
        format = new { type = "object", properties = new { intent = new { type = "string" } }, required = new[] { "intent" } },
        options = new { temperature = 0, num_predict = 32 } };

    [Fact]
    public async Task Explicit_Gemini_provider_sends_key_only_in_header_and_preserves_structured_gateway_contract()
    {
        using var client = Client("Gemini", async request =>
        {
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", request.RequestUri!.AbsoluteUri);
            Assert.Equal("synthetic-test-key", request.Headers.GetValues("x-goog-api-key").Single());
            Assert.Null(request.Headers.Authorization);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("Classify a synthetic question.", body.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal("Where is the demo parcel?", body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
            var config = body.RootElement.GetProperty("generationConfig");
            Assert.Equal(32, config.GetProperty("maxOutputTokens").GetInt32());
            Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
            Assert.True(config.TryGetProperty("responseJsonSchema", out _));
            return Json(HttpStatusCode.OK, """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"intent\":\"STATUS\"}"}]}}],"modelVersion":"gemini-3.5-flash-lite"}""");
        });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(envelope.GetProperty("done").GetBoolean());
        Assert.Equal("{\"intent\":\"STATUS\"}", envelope.GetProperty("response").GetString());
    }

    [Fact]
    public async Task Default_Ollama_provider_keeps_requests_local_without_a_Gemini_key()
    {
        using var client = Client("Ollama", request =>
        {
            Assert.Equal("http://127.0.0.1:11434/api/generate", request.RequestUri!.AbsoluteUri);
            Assert.False(request.Headers.Contains("x-goog-api-key"));
            return Task.FromResult(Json(HttpStatusCode.OK, """{"done":true,"response":"{}"}"""));
        });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Quota_error_is_returned_without_retrying_or_falling_back_to_invented_output()
    {
        var calls = 0;
        using var client = Client("Gemini", _ => { calls++; return Task.FromResult(Json(HttpStatusCode.TooManyRequests, "{}")); });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"candidates":[null]}""")]
    [InlineData("""{"candidates":[{"finishReason":7,"content":{"parts":[]}}]}""")]
    [InlineData("""{"candidates":[{"finishReason":"STOP","content":{"parts":[null]}}]}""")]
    [InlineData("""{"candidates":[{"finishReason":"MAX_TOKENS","content":{"parts":[{"text":"{}"}]}}]}""")]
    public async Task Incomplete_or_blocked_output_cannot_be_accepted_as_complete_generation(string body)
    {
        using var client = Client("Gemini", _ => Task.FromResult(Json(HttpStatusCode.OK, body)));
        await Assert.ThrowsAsync<JsonException>(() => client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation));
    }

    [Fact]
    public async Task Assistant_text_is_not_sent_to_cloud_under_the_original_local_only_policy()
    {
        var calls = 0;
        using var client = new HttpClient(new GeminiModelHandler(Options.Create(new ModelProviderOptions
        { Provider = "Gemini", GeminiApiKey = "synthetic-test-key" }), assistant: true)
        { InnerHandler = new Probe(_ => { calls++; return Task.FromResult(Json(HttpStatusCode.OK, "{}")); }) });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Provider_configuration_is_explicit_and_requires_server_credentials_for_Gemini()
    {
        Assert.True(new ModelProviderOptions().HasValidConfiguration());
        Assert.False(new ModelProviderOptions { Provider = "Gemini" }.HasValidConfiguration());
        Assert.False(new ModelProviderOptions { Provider = "Unknown" }.HasValidConfiguration());
    }

    [Theory]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Opted_in_transient_failure_retries_once_with_a_fresh_identical_request(int status)
    {
        var requests = new List<HttpRequestMessage>();
        var bodies = new List<string>();
        using var client = RetryClient(async (request, ct) =>
        {
            requests.Add(request);
            bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("synthetic-test-key", request.Headers.GetValues("x-goog-api-key").Single());
            return requests.Count == 1 ? Json((HttpStatusCode)status, "{}") : SuccessfulIntent();
        });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        response.EnsureSuccessStatusCode();
        Assert.Equal(2, requests.Count);
        Assert.NotSame(requests[0], requests[1]);
        Assert.Equal(bodies[0], bodies[1]);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("{\"intent\":\"STATUS\"}", envelope.GetProperty("response").GetString());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Retry_does_not_replay_nontransient_or_quota_failures(int status)
    {
        var calls = 0;
        using var client = RetryClient((_, _) => { calls++; return Task.FromResult(Json((HttpStatusCode)status, "{}")); });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Persistent_transient_failure_stops_after_two_calls()
    {
        var calls = 0;
        using var client = RetryClient((_, _) => { calls++; return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}")); });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Provider_retry_after_is_respected_without_an_early_replay()
    {
        var calls = 0;
        using var client = RetryClient((_, _) =>
        {
            calls++;
            var response = Json(HttpStatusCode.ServiceUnavailable, "{}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return Task.FromResult(response);
        });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancelled_caller_never_starts_a_second_provider_request()
    {
        using var cancelled = new CancellationTokenSource();
        var calls = 0;
        using var client = RetryClient((_, _) =>
        {
            calls++; cancelled.Cancel();
            return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}"));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation, cancelled.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Stalled_first_attempt_is_cancelled_before_one_recovery_call()
    {
        var clock = new RecoveryClock();
        var calls = 0;
        using var client = RetryClient(async (_, ct) =>
        {
            if (++calls == 1) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return SuccessfulIntent();
        }, clock);
        var pending = client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        await clock.FireNextAsync(); // First attempt expires.
        await clock.FireNextAsync(); // Backoff completes.
        using var response = await pending;
        response.EnsureSuccessStatusCode();
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Retry_is_off_by_default_and_never_replays_malformed_success()
    {
        var calls = 0;
        using (var client = Client("Gemini", _ => { calls++; return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}")); }))
        using (var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, calls);
        calls = 0;
        using var retry = RetryClient((_, _) => { calls++; return Task.FromResult(Json(HttpStatusCode.OK, "{\"candidates\":[]}")); });
        await Assert.ThrowsAsync<JsonException>(() => retry.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Stalled_success_body_is_cancelled_and_disposed_before_recovery()
    {
        var clock = new RecoveryClock();
        var calls = 0;
        using var stream = new StalledBody();
        using var client = RetryClient((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }
            : SuccessfulIntent()), clock);
        var pending = client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        await clock.FireNextAsync();
        await clock.FireNextAsync();
        using var response = await pending;
        response.EnsureSuccessStatusCode();
        Assert.Equal(2, calls);
        Assert.True(stream.WasDisposed);
    }

    [Fact]
    public async Task Stalled_second_attempt_exits_without_starting_a_third_request()
    {
        var clock = new RecoveryClock();
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var client = RetryClient(async (_, ct) =>
        {
            if (++calls == 1) return Json(HttpStatusCode.ServiceUnavailable, "{}");
            secondStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return SuccessfulIntent();
        }, clock);
        var pending = client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        await clock.FireNextAsync(); // Backoff, earlier than the first attempt's disposed timer.
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await clock.FireNextAsync(); // Second attempt expires.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(2, calls);
    }

    private sealed class StalledBody : Stream
    {
        public bool WasDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }

    private static HttpResponseMessage SuccessfulIntent() => Json(HttpStatusCode.OK,
        """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"intent\":\"STATUS\"}"}]}}]}""");
    private static HttpClient RetryClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, TimeProvider? clock = null)
        => new(new GeminiModelHandler(Options.Create(new ModelProviderOptions
        { Provider = "Gemini", GeminiApiKey = "synthetic-test-key", GeminiRetryTransientFailures = true }), clock: clock)
        { InnerHandler = new CancellableProbe(send) });
    private sealed class CancellableProbe(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }

    private sealed class RecoveryClock : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ManualTimer> timers = [];
        private readonly SemaphoreSlim created = new(0);
        private long ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (gate) timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
        public async Task FireNextAsync()
        {
            while (true)
            {
                ManualTimer? next;
                lock (gate)
                {
                    next = timers.Where(t => t.Active).OrderBy(t => t.Due).FirstOrDefault();
                    if (next != null) { ticks = next.Due; next.Active = false; }
                }
                if (next != null) { next.Callback(next.State); return; }
                if (!await created.WaitAsync(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("The transport did not schedule the expected timer.");
            }
        }
        private sealed class ManualTimer(RecoveryClock owner, TimerCallback callback, object? state) : ITimer
        {
            public TimerCallback Callback => callback;
            public object? State => state;
            public long Due { get; private set; }
            public bool Active { get; set; }
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner.gate)
                {
                    if (disposed) return false;
                    Due = owner.ticks + dueTime.Ticks;
                    Active = dueTime != Timeout.InfiniteTimeSpan;
                }
                owner.created.Release();
                return true;
            }
            public void Dispose() { lock (owner.gate) { disposed = true; Active = false; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private static HttpClient Client(string provider, Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        => new(new GeminiModelHandler(Options.Create(new ModelProviderOptions { Provider = provider, GeminiApiKey = "synthetic-test-key" }))
        { InnerHandler = new Probe(send) });
    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json) };
    private sealed class Probe(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
