# Cirreum Runtime Invocation WebSockets

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Runtime.Invocation.WebSockets.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Invocation.WebSockets/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Runtime.Invocation.WebSockets.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Invocation.WebSockets/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Runtime.Invocation.WebSockets?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Runtime.Invocation.WebSockets/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Runtime.Invocation.WebSockets?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Runtime.Invocation.WebSockets/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Runtime Extensions package for the Cirreum WebSocket invocation source.**

## Overview

`Cirreum.Runtime.Invocation.WebSockets` is the L5 Runtime Extensions package that surfaces app-facing extension methods for wiring raw WebSocket endpoints into Cirreum's unified `IInvocationContext` seam. It supplies three extensions, each on the framework type that makes them most discoverable, plus a slim builder for the optional companion request endpoint:

- `AddWebSocketInvocation()` on `IHostApplicationBuilder` — registers the WebSocket invocation source (marker-dedup'd) and opens the `IInvocationBuilder` scope for per-instance handler bindings.
- `AddWebSocket<THandler>(instanceKey, request: ...)` on `IInvocationBuilder` — captures `THandler` at the call site, registers it as scoped, and (optionally) configures the companion HTTP request endpoint via the `request:` builder.
- `MapWebSocketInvocation()` on `IEndpointRouteBuilder` — invokes every `InvocationProviderMapping` whose `ProviderName` matches `WebSocketInvocationRegistrar.ProviderKey`, walking enabled instances and mapping each WebSocket endpoint (and its optional request endpoint) at the configured paths. Calls `app.UseWebSockets()` automatically.
- `IWebSocketRequestBuilder` — slim, framework-specific minimal API surface for the request endpoint with `Map(handler, configure?)` (defaults to POST) and `Map(httpMethod, handler, configure?)` (explicit method) overloads.

Apps install this package directly. It transitively pulls the L3 `Cirreum.Invocation.WebSockets` (registrar, orchestrator, `IWebSocketConnection` interface and impl, `IWebSocketUrlBuilder`) and the L4 `Cirreum.Runtime.InvocationProvider` (helper, scope object).

## Architectural position

```
L2 Core
  Cirreum.InvocationProvider               ← abstractions: IInvocationContext, registrar base, ...

L3 Infrastructure
  Cirreum.Invocation.SignalR               ← peer for SignalR Hubs
  Cirreum.Invocation.WebSockets            ← registrar, orchestrator, connection adapter

L4 Runtime
  Cirreum.Runtime.InvocationProvider       ← IInvocationBuilder scope object, RegisterInvocationProvider helper

L5 Runtime Extensions
  Cirreum.Runtime.Invocation.SignalR       ← peer for SignalR
  Cirreum.Runtime.Invocation.WebSockets    ← THIS PACKAGE
  Cirreum.Runtime.Invocation               ← umbrella (AddInvocation, MapInvocation across all sources)
```

Mirrors the SignalR L5 package's shape — same SRP-split, same per-source/umbrella relationship, same `Add*Invocation` + per-instance generic-method-with-key + `Map*Invocation` pattern.

## What's in the box

| Extension / Type | Lives on | Role |
|---|---|---|
| `AddWebSocketInvocation(this IHostApplicationBuilder, Action<IInvocationBuilder>?)` (`Microsoft.Extensions.Hosting`) | `IHostApplicationBuilder` | Top-level entry point. Marker-dedup'd registration of the WebSocket invocation source; opens the `IInvocationBuilder` scope for per-instance handler bindings. |
| `AddWebSocket<THandler>(this IInvocationBuilder, string, Action<IWebSocketRequestBuilder>?)` (`Cirreum.Invocation`) | `IInvocationBuilder` | Per-instance handler binding. Captures `THandler` at the call site, registers it as scoped, optionally configures the companion request endpoint via the `request:` builder. |
| `IWebSocketRequestBuilder` (`Cirreum.Invocation`) | (passed as `request:` callback parameter) | Slim minimal API surface for the request endpoint. `Map(handler, configure?)` defaults to POST; `Map(httpMethod, handler, configure?)` for explicit methods. The optional `configure` callback hooks into the real `RouteHandlerBuilder` for OpenAPI / naming / tags / additional metadata. |
| `MapWebSocketInvocation(this IEndpointRouteBuilder)` (`Microsoft.AspNetCore.Builder`) | `IEndpointRouteBuilder` | Endpoints-phase entry point. Calls `app.UseWebSockets()`, resolves WebSocket-tagged `InvocationProviderMapping` records, and invokes their deferred `Map` closures. |

## How registration works

The `AddWebSocketInvocation()` extension does three things:

1. Marker-dedup'd: binds the provider's `WebSocketOptions` sub-section to `IOptions<WebSocketOptions>` via `services.Configure<WebSocketOptions>(section)`. The ASP.NET WebSocket middleware reads this from DI at request time, so `MapWebSocketInvocation()`'s call to `app.UseWebSockets()` automatically picks up provider-level `KeepAliveInterval`, `KeepAliveTimeout`, `AllowedOrigins` defaults — no hand-rolled `app.UseWebSockets(options)` call required.
2. Marker-dedup'd: registers the WebSocket invocation source by calling `builder.RegisterInvocationProvider<WebSocketInvocationRegistrar, WebSocketInvocationSettings, WebSocketInvocationInstanceSettings>()` from the L4 helper. The L4 helper:
   - Binds `Cirreum:Invocation:Providers:WebSocket` from `IConfiguration` to `WebSocketInvocationSettings`.
   - Calls `registrar.Register(...)` — services phase — which binds `IOptions<WebSocketInvocationSettings>`, registers `IWebSocketUrlBuilder`, registers `WebSocketOrchestrator`, and validates per-instance settings (paths, hard caps).
   - Stashes an `InvocationProviderMapping` in DI capturing the deferred `registrar.Map(...)` closure.
3. Opens the `IInvocationBuilder` scope for the configure callback so apps can chain `AddWebSocket<THandler>(instanceKey, request: ...)` calls per handler-type.

Inside the configure callback, each `AddWebSocket<THandler>(instanceKey, request: ...)` call:

1. Validates `THandler` uniqueness — the same handler type cannot be mapped to two instance keys (throws on conflict).
2. Resolves the `request:` builder if provided — captures the handler delegate, optional method override, and optional `configure` callback into a `WebSocketRequestBuilder`. Throws if the callback runs but never invokes `Map(...)`.
3. Stashes a `WebSocketHandlerMapping(instanceKey, typeof(THandler), requestHandler, requestMethod, configureRequestRoute)` singleton in DI.
4. Registers `THandler` as a scoped service — one instance per connection.

`MapWebSocketInvocation()` calls `app.UseWebSockets()` then resolves all `InvocationProviderMapping` records with `ProviderName == WebSocketInvocationRegistrar.ProviderKey` and invokes their `Map` closures. The L3 registrar's `MapSource`:

1. Validates `RequestPath` ↔ `request:` builder pairing — throws at startup with an actionable message if either is set without the other.
2. Maps the WebSocket endpoint at `Path` using `Map()` so both GET (HTTP/1.1) and CONNECT (HTTP/2+) are accepted; excludes from OpenAPI/Swagger discovery.
3. Maps the request endpoint at `RequestPath` (when configured) using `MapMethods` with the captured method (default POST), attaches `WebSocketInstanceMetadata` for `IWebSocketUrlBuilder`, and invokes the app's `configure` callback against the real `RouteHandlerBuilder`.
4. Applies `RequireAuthorization` to both endpoints when `Scheme` is set.

## Configuration

WebSocket exposes two configuration surfaces — Cirreum mirrors each as an explicit, named sub-section. Cirreum framework fields (`Enabled`, `Path`, `Scheme`, etc.) live at the instance-section root; ASP.NET-native `WebSocketOptions` provider-wide defaults live in a sibling `WebSocketOptions` sub-section so the two roles never collide.

```json
{
  "Cirreum": {
    "Invocation": {
      "Providers": {
        "WebSocket": {

          "WebSocketOptions": {
            "KeepAliveInterval": "00:02:00",
            "KeepAliveTimeout":  "00:00:30",
            "AllowedOrigins":    [ "https://app.example.com" ]
          },

          "Instances": {

            "media": {
              "Enabled": true,
              "Path": "/twilio/media-stream/{callSid}",
              "RequestPath": "/twilio/incoming-call",
              "Scheme": "twilio",
              "DisconnectTimeoutSeconds": 60,
              "MaxMessageSizeBytes": 65536,
              "KeepAliveInterval": "00:00:30",
              "KeepAliveTimeout": "00:00:10"
            },

            "telemetry": {
              "Enabled": true,
              "Path": "/ws/telemetry",
              "Scheme": "oidc_primary",
              "MaxMessageSizeBytes": 524288
            }

          }
        }
      }
    }
  }
}
```

| Sub-section | Binds to | Scope |
|---|---|---|
| `Providers:WebSocket:WebSocketOptions` | ASP.NET `WebSocketOptions` (provider-wide) | Defaults applied to the WebSocket middleware process-wide via `IOptions<WebSocketOptions>` |
| `Providers:WebSocket:Instances:{key}` | `WebSocketInvocationInstanceSettings` | Per-instance Cirreum-framework fields (Path, RequestPath, Scheme, …) and per-connection overrides |

### Per-instance fields

| Field | Default | Hard cap | Purpose |
|---|---|---|---|
| `Enabled` | `false` | — | Per-instance gate |
| `Path` | (required) | — | WebSocket endpoint route template (supports `{name}` placeholders) |
| `RequestPath` | null | — | Optional companion HTTP endpoint that initiates the WebSocket flow |
| `Scheme` | null | — | References a configured Authorization instance; applies `RequireAuthorization` to both endpoints |
| `DisconnectTimeoutSeconds` | 30 | 300 | Cleanup budget for `OnDisconnectedAsync` hooks |
| `MaxMessageSizeBytes` | 64 KB | 8 MB | Max bytes per complete message; oversize → `MessageTooBig` close |
| `ReceiveBufferSizeBytes` | 4 KB | 64 KB | Initial pooled receive buffer per connection |
| `KeepAliveInterval` | null | — | Per-connection override of provider-level `WebSocketOptions.KeepAliveInterval` (default 2 min). Applied at `AcceptWebSocketAsync` time for this instance only |
| `KeepAliveTimeout` | null | — | Per-connection override of provider-level `WebSocketOptions.KeepAliveTimeout` (default 30 s). Applied at `AcceptWebSocketAsync` time for this instance only |

`Scheme` references a configured Authorization instance under `Cirreum:Authorization:Providers:*:Instances:{Scheme}`. Optional — leave unset for unauthenticated endpoints (rare).

### Provider-level `WebSocketOptions`

`AddWebSocketInvocation()` binds this sub-section directly to ASP.NET's `IOptions<WebSocketOptions>` (via `services.Configure<WebSocketOptions>(section)`). The WebSocket middleware reads from DI, so `MapWebSocketInvocation()`'s call to `app.UseWebSockets()` automatically picks them up — no separate `app.UseWebSockets(options)` call required.

| Field | Default | Purpose |
|---|---|---|
| `KeepAliveInterval` | 2 minutes | Protocol-level ping interval (provider-wide default) |
| `KeepAliveTimeout` | 30 seconds | Abort the connection if no pong within this time (provider-wide default) |
| `AllowedOrigins` | (empty — no filtering) | Origin header allowlist for CSWSH defense. **Low-priority for Cirreum apps** — the API-first/cookieless design eliminates the attack surface; only relevant if mixing cookie auth and WebSocket |

Programmatic configuration (`services.Configure<WebSocketOptions>(o => ...)`) still works alongside the JSON binding — the options system merges sources naturally. Apps still wanting to call `app.UseWebSockets(options)` themselves with a hand-built options instance can do so before `MapWebSocketInvocation()`; that bypasses the DI-resolved options and uses the passed instance.

## Quick start — telemetry endpoint (single-phase)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddWebSocketInvocation(b => b
    .AddWebSocket<TelemetryHandler>("telemetry"));

var app = builder.Build();
app.MapWebSocketInvocation();
app.Run();
```

```csharp
public sealed class TelemetryHandler : WebSocketHandler {

    public override async Task OnMessageAsync(
        IInvocationContext context,
        ReadOnlyMemory<byte> message,
        WebSocketMessageType messageType) {

        var batch = JsonSerializer.Deserialize<TelemetryBatch>(message.Span);
        await ProcessAsync(batch, context.Aborted);
        await SendAsync("Ack", new { count = batch.Items.Count }, context.Aborted);
    }

}
```

## Quick start — Twilio Media Streams (two-phase)

The IVA reference codebase pattern: Twilio webhooks the incoming-call URL, our server returns TwiML with a WebSocket URL, Twilio opens the WebSocket and streams audio frames.

**Configuration** — see the JSON above for the `media` instance.

**Program.cs:**
```csharp
var builder = DomainApplication.CreateBuilder(args);

builder.AddWebSocketInvocation(b => b
    .AddWebSocket<TwilioMediaHandler>("media", request: r => r
        .Map(TwilioApi.HandleRequest, m => m
            .WithName("twilio-incoming-call")
            .WithTags("twilio")
            .Produces<string>(200, "text/xml"))));

using var app = builder.Build<MyDomainMarker>();
app.UseDefaultMiddleware();
app.MapWebSocketInvocation();
await app.RunAsync();
```

**Request delegate** — full minimal API binding; `IWebSocketUrlBuilder` builds the absolute `wss://` URL with `{callSid}` auto-extracted from form data:

```csharp
public static class TwilioApi {

    public static async Task<IResult> HandleRequest(
        HttpContext context,
        IWebSocketUrlBuilder urls,
        ITwilioRequestValidator validator,
        DomainApiClient domainApi) {

        if (!validator.ValidateRequest(context)) return Results.Unauthorized();

        var callSid = context.Request.Form["CallSid"].ToString();
        var session = await domainApi.ClaimSessionAsync(callSid);
        if (!session.IsSuccess) return Results.Content(HangupTwiml(), "text/xml");

        // {callSid} auto-extracted from form data (case-insensitive name match)
        var streamUrl = urls.Build(context);

        return Results.Content($"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Response>
                <Connect><Stream url="{streamUrl}" /></Connect>
            </Response>
            """, "text/xml");
    }

}
```

**Handler:**
```csharp
// Note the symmetry: handler-bound `this.SendAsync(...)` for typed JSON to the inbound
// (Twilio) socket; for binary audio frames, call `this.Connection.SendBytesAsync(...)`
// directly (Connection is typed as IWebSocketConnection — no cast). The outbound
// (handler-owned) AI socket uses its own SendAsync directly. Cross-cutting code (Conductor
// handlers triggered from AI tool calls) reaches the inbound connection through
// accessor.Current?.Connection — see "Server-initiated push".
public sealed class TwilioMediaHandler(
    IAiClient ai,
    IDomainApi domain,
    ILogger<TwilioMediaHandler> logger) : WebSocketHandler {

    private ClientWebSocket? _aiSocket;
    private Task? _aiTask;
    private CallDisposition _disposition = CallDisposition.Resolved;

    public override async Task OnConnectedAsync(CancellationToken ct) {
        _aiSocket = await ai.ConnectAsync(ct);

        _aiTask = Task.Run(async () => {
            try {
                await foreach (var aiEvent in ReadAiEventsAsync(_aiSocket, ct)) {
                    if (TryExtractAudio(aiEvent, out var streamSid, out var audio)) {
                        var msg = TwilioMediaMessage.Create(streamSid, audio);
                        await SendAsync(msg, ct);   // ← handler-bound, works in this Task.Run
                    }
                }
            } finally {
                // AI side ended — terminate the inbound transport too
                Connection!.Abort();
            }
        }, ct);
    }

    public override async Task OnMessageAsync(
        IInvocationContext context,
        ReadOnlyMemory<byte> message,
        WebSocketMessageType messageType) {

        // Forward Twilio media frames to the AI socket. _aiSocket is handler-owned —
        // direct SendAsync; no framework involvement.
        await _aiSocket!.SendAsync(message, messageType, endOfMessage: true, context.Aborted);
    }

    public override async Task OnDisconnectedAsync(DisconnectInfo info, CancellationToken ct) {
        // ct is a bounded cleanup budget (default 30s, or host shutdown). On exhaustion, bail.
        if (info.Exception is not null) {
            _disposition = CallDisposition.Error;
            logger.LogError(info.Exception, "Call ended with error: {Reason}", info.Reason);
        }

        if (_aiSocket?.State == WebSocketState.Open) {
            try {
                await _aiSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Call ended", ct);
            } catch (OperationCanceledException) {
                logger.LogWarning("Cleanup budget exhausted closing AI socket");
                return;
            } catch (Exception ex) {
                logger.LogError(ex, "Error closing AI socket");
            }
        }

        if (_aiTask is not null) await _aiTask;

        await domain.CompleteCallAsync(info, _disposition, ct);
    }

}
```

## The `IWebSocketRequestBuilder` — slim minimal API surface

```csharp
public interface IWebSocketRequestBuilder {
    void Map(Delegate handler, Action<RouteHandlerBuilder>? configure = null);
    void Map(string httpMethod, Delegate handler, Action<RouteHandlerBuilder>? configure = null);
}
```

| Overload | Default | Use |
|---|---|---|
| `Map(Delegate, configure?)` | `POST` | Webhook-style endpoints (Twilio, Stripe, GitHub all use POST). |
| `Map(string method, Delegate, configure?)` | (explicit) | GET-style negotiation, query-string token flows, or other rare cases. |

**The `Delegate` parameter** accepts any minimal API delegate — inline lambdas, static method groups, instance method groups. Full minimal API parameter binding applies (DI services, `HttpContext`, route parameters, `IFormFile`, body binding, `IResult` return types, etc.):

```csharp
// Inline lambda — DI services as parameters
.Map(async (HttpContext ctx, ISessionService sessions) => { ... })

// Static method group
.Map(TwilioApi.HandleRequest)

// Instance method group
.Map(_voiceApi.HandleRequest)
```

**The `configure` callback** hooks into the real `RouteHandlerBuilder` at endpoints-phase time. Inside it, every minimal API extension works: `WithName`, `WithTags`, `Produces<T>`, `Accepts<T>`, `WithOpenApi`, `RequireAuthorization` (additional schemes beyond the instance `Scheme`), `DisableAntiforgery`, `WithMetadata`, etc.

```csharp
.Map(TwilioApi.HandleRequest, m => m
    .WithName("twilio-incoming-call")
    .WithTags("twilio")
    .Accepts<IFormCollection>("application/x-www-form-urlencoded")
    .Produces<string>(200, "text/xml")
    .WithOpenApi(op => {
        op.Description = "Twilio Programmable Voice incoming-call webhook.";
        return op;
    }))
```

## `IWebSocketUrlBuilder` — building WebSocket URLs from path templates

For apps that need to embed the WebSocket URL in a request response (TwiML, JSON, etc.), inject `IWebSocketUrlBuilder`:

```csharp
public interface IWebSocketUrlBuilder {
    string Build(HttpContext context, object? routeValues = null, object? queryValues = null);
    string Build(string instanceKey, HttpContext context, object? routeValues = null, object? queryValues = null);
}
```

| Overload | Instance source |
|---|---|
| `Build(HttpContext, ...)` | Implicit — resolved from the active endpoint's `WebSocketInstanceMetadata` (only works inside a request endpoint). |
| `Build(string instanceKey, HttpContext, ...)` | Explicit — for cross-instance scenarios or use outside a request endpoint. |

Path-template values are resolved in priority order:

1. Explicit `routeValues` parameter
2. `Request.RouteValues` (URL route parameters)
3. `Request.Query` (query string)
4. `Request.Form` (form fields, only when content-type is form-encoded)

Names match **case-insensitively** — Twilio's `CallSid` form field auto-fills the `{callSid}` template placeholder. Scheme converts automatically: `https`→`wss`, `http`→`ws`. Unresolved placeholders throw at build time rather than producing malformed URLs.

## Server-initiated push

Three flavors, all routing to the same calling client. Pick by audience and payload shape.

### `this.SendAsync(...)` — handler-bound, typed JSON

Two protected overloads on `WebSocketHandler`:

```csharp
protected ValueTask SendAsync<T>(T payload, CancellationToken ct = default);
protected ValueTask SendAsync<T>(string method, T payload, CancellationToken ct = default);
```

Thin forwarders to `Connection.SendAsync(...)`. Both send `WebSocketMessageType.Text` frames. JSON-serialization uses `SerializerOptions` (captured onto the connection at upgrade — override `SerializerOptions` to wire in a source-gen `JsonTypeInfoResolver`). **No accessor lookup, no `IInvocationContextAccessor` dependency** — works from any calling context inside the handler, including handler-managed `Task.Run` background loops, timers, and fire-and-forget continuations. The IVA-style voice example above uses this for the audio-frame hot path:

```csharp
await SendAsync(twilioMediaMessage, ct);   // ← from inside Task.Run, just works
```

Use this for **handler-internal typed-JSON push** — the common case for WebSocket apps that own outbound state and forward it to the inbound caller (voice, telemetry, custom protocols).

### `IInvocationConnection.SendAsync<T>` — ambient, typed JSON (cross-cutting code)

From cross-cutting code that doesn't know its transport — Conductor command/query handlers, validators, transport-agnostic services — reach the connection through the ambient `IInvocationContextAccessor.Current`. The same handler code can push back over HTTP, SignalR, or WebSocket without knowing which it's running on:

```csharp
public sealed class GenerateReportHandler(IInvocationContextAccessor accessor)
    : ICommandHandler<GenerateReportCommand> {

    public async ValueTask<Result> Handle(GenerateReportCommand cmd, CancellationToken ct) {
        var connection = accessor.Current?.Connection;
        if (connection is not null) {
            await connection.SendAsync("Progress", new { Percent = 0, Stage = "Loading" }, ct);
        }
        // ... work ...
        if (connection is not null) {
            await connection.SendAsync("Progress", new { Percent = 100, Stage = "Done" }, ct);
        }
        return Result.Success(/* ... */);
    }

}
```

Same command handler runs from HTTP, SignalR, or WebSocket — the seam unifies them. The HTTP caller gets only the return value (the connection is `null`); the SignalR / WebSocket caller gets the progress stream *and* the return value. Cross-cutting code automatically picks up the handler's `SerializerOptions` (the connection captures them at upgrade time) — including any source-generated `JsonTypeInfoResolver` the app configured.

### `IWebSocketConnection.SendBytesAsync` — raw frame writes (binary protocols, audio)

Inside a handler, call directly on `this.Connection` — the property is typed as `IWebSocketConnection` (which extends `IInvocationConnection`), so binary frames need no cast:

```csharp
public sealed class TwilioMediaHandler : WebSocketHandler {

    public override async Task OnMessageAsync(
        IInvocationContext context,
        ReadOnlyMemory<byte> message,
        WebSocketMessageType messageType) {

        var (audioChunk, mark) = TwilioMessage.Parse(message);

        // Typed JSON via the handler shortcut
        await SendAsync(mark, context.Aborted);

        // Raw bytes — straight on Connection, no cast
        await this.Connection.SendBytesAsync(audioChunk, WebSocketMessageType.Binary, context.Aborted);
    }

}
```

From cross-cutting WebSocket-aware code (which holds only the L2 `IInvocationConnection?` view), the cast IS the explicit "I'm using WebSocket-specific behavior" acknowledgment:

```csharp
if (accessor.Current?.Connection is IWebSocketConnection ws) {
    await ws.SendBytesAsync(audioChunk, WebSocketMessageType.Binary, ct);
}
```

### When to use which

| You're writing | Use |
|---|---|
| Typed JSON push from inside a `WebSocketHandler` (any lifecycle hook or background task) | **`this.SendAsync(payload, ct)`** / **`this.SendAsync(method, payload, ct)`** — handler shortcut |
| Typed JSON push from cross-cutting code (Conductor handlers, validators, transport-agnostic services) | **`accessor.Current?.Connection?.SendAsync(...)`** — ambient connection, transport-agnostic |
| Raw frame writes (binary protocols, audio, pre-serialized payloads) — handler internal | **`this.Connection.SendBytesAsync(...)`** — `Connection` is typed as `IWebSocketConnection`, no cast |
| Raw frame writes from cross-cutting WebSocket-aware code | **`(accessor.Current?.Connection as IWebSocketConnection)?.SendBytesAsync(...)`** — explicit downcast acknowledges WebSocket-specific behavior |

The three complement perfectly for the AI tool-call story: typed JSON acks stay on `this.SendAsync(...)` (handler-bound, hot path); audio frames go through `this.Connection.SendBytesAsync(...)`; AI tool calls dispatched through Conductor reach back via `accessor.Current?.Connection?.SendAsync(...)` for progress streaming, under the same authenticated identity that the WebSocket was opened with — and using the same handler-configured source-gen JSON.

### What none of these APIs does

All push only to the *currently-executing* connection. Broadcast / target-by-ConnectionId / target-by-group is **not** built into raw WebSocket today — see [BACKLOG](docs/BACKLOG.md) "Connection registry / fan-out push." For those patterns, use SignalR (`Cirreum.Runtime.Invocation.SignalR`) — it has them natively via `IHubContext<THub>.Clients.X.SendAsync(...)`. Raw WebSocket is appropriate for streaming pipelines where each connection is independent (voice, telemetry); SignalR is appropriate when broadcast/group/presence are core requirements.

## Connection lifecycle

Implement `IConnectionLifecycle` (from `Cirreum.Invocation.Connections`) and register it in DI to receive cross-cutting `OnConnectedAsync` / `OnDisconnectedAsync` callbacks across all WebSocket connections (and other long-lived sources). The orchestrator dispatches both under a synthetic invocation scope so consumers like `IUserStateAccessor` work normally inside the callbacks.

```csharp
internal sealed class AuditConnectionLifecycle(ILogger<AuditConnectionLifecycle> logger)
    : IConnectionLifecycle {

    public ValueTask<bool> OnConnectedAsync(IInvocationConnection connection, CancellationToken ct) {
        // Inspect connection.User, connection.ConnectionId, connection.Items, etc.
        // Return false to reject the connection (orchestrator aborts the WebSocket).
        return ValueTask.FromResult(true);
    }

    public ValueTask OnDisconnectedAsync(
        IInvocationConnection connection,
        DisconnectInfo info,
        CancellationToken ct) {

        if (info.WasGraceful) {
            logger.LogInformation("Connection {Id} closed cleanly", connection.ConnectionId);
        } else if (info.Exception is not null) {
            logger.LogWarning(info.Exception,
                "Connection {Id} aborted: {Reason}", connection.ConnectionId, info.Reason);
        }

        return ValueTask.CompletedTask;
    }

}
```

Per-transport mapping for `DisconnectInfo`: `WasGraceful = closeStatus == WebSocketCloseStatus.NormalClosure`, `Reason = closeStatusDescription`, `Exception` populated when the loop exited due to a thrown exception.

The `cancellationToken` on `OnDisconnectedAsync` is a **bounded cleanup budget** — fires on either the configured `DisconnectTimeoutSeconds` (default 30 s) or `IHostApplicationLifetime.ApplicationStopping`, whichever comes first. Pass it directly into cancellable cleanup calls.

## Connection termination from the handler

For multi-socket orchestration patterns (Twilio + AI socket; client + downstream worker), the handler can terminate the inbound WebSocket from inside any lifecycle hook by calling `Connection.Abort()`:

```csharp
public override async Task OnConnectedAsync(CancellationToken ct) {
    _aiSocket = await ConnectToAiAsync(ct);

    _aiTask = Task.Run(async () => {
        try {
            await ReadFromAiAsync(_aiSocket, ct);
        } finally {
            Connection!.Abort();   // ← terminate inbound when AI side ends
        }
    }, ct);
}
```

`Abort()` cancels the linked CTS that the orchestrator's `WebSocket.ReceiveAsync` is waiting on — the receive throws, the frame loop exits cleanly, and `OnDisconnectedAsync` runs as if the close was orderly.

## Subprotocol negotiation

Override `OnSelectSubProtocolAsync` to negotiate a WebSocket subprotocol (e.g. wire-format version negotiation, app-specific protocols). Read the requested subprotocols from `HttpContext.WebSockets.WebSocketRequestedProtocols` and return the chosen value (must be one of the offered values, or `null` for no subprotocol):

```csharp
public override Task<string?> OnSelectSubProtocolAsync(HttpContext context) {
    var requested = context.WebSockets.WebSocketRequestedProtocols;
    return Task.FromResult(
        requested.Contains("cirreum-v2") ? "cirreum-v2"
      : requested.Contains("cirreum-v1") ? "cirreum-v1"
      : null);
}
```

After accept, the negotiated value is exposed via `Connection.SubProtocol` (read-only) for the remainder of the connection's lifetime.

## Dependencies

- **Cirreum.Runtime.InvocationProvider** `1.1.0+` — L4 helper (`IInvocationBuilder` scope object, `RegisterInvocationProvider<>` helper, `InvocationProviderMapping` record)
- **Cirreum.Invocation.WebSockets** `1.2.0+` — L3 registrar, orchestrator, `IWebSocketConnection` interface + connection adapter (with typed `SendAsync<T>` and `SendBytesAsync`), `IWebSocketUrlBuilder`, `WebSocketHandler` base
- **Microsoft.AspNetCore.App** (framework reference) — WebSocket (`Microsoft.AspNetCore.WebSockets`), endpoint routing, hosting

## Versioning

Follows [Semantic Versioning](https://semver.org/). Major bumps are coordinated with the L3 `Cirreum.Invocation.WebSockets` and the L2 `Cirreum.InvocationProvider` packages.

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
