# Cirreum.Runtime.Invocation.WebSockets 1.0.0 — app-facing WebSocket registration

Initial release of the L5 Runtime Extensions package for the Cirreum WebSocket invocation source. Provides `AddWebSocketInvocation()`, `AddWebSocket<THandler>()`, and `MapWebSocketInvocation()` — the three call sites apps need in `Program.cs` to surface raw WebSocket endpoints as Cirreum invocation sources.

Pairs with `Cirreum.Invocation.WebSockets 1.0.0` (the L3 transport adapter); apps reference this package directly and the L3 flows in transitively.

---

## Why this release exists

The L3 package (`Cirreum.Invocation.WebSockets`) holds the framework machinery — registrars, middleware, frame loop, `IConnectionSender` impl. Apps don't need to know about any of that. The L5 layer is the seam where app developers actually live: a small set of fluent extensions that wire everything from configuration plus a one-line endpoint mapping.

This split mirrors the SignalR family pattern (`Cirreum.Invocation.SignalR` L3 + `Cirreum.Runtime.Invocation.SignalR` L5) — apps reference one Runtime Extensions package per transport they use; framework internals stay invisible.

---

## What's new

### `AddWebSocketInvocation(builder, configure)` — services-phase registration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddWebSocketInvocation(b => b
    .AddWebSocket<TwilioMediaHandler>("media", request: r => r
        .Map(TwilioApi.HandleRequest, m => m
            .WithName("twilio-incoming-call")
            .WithTags("twilio")
            .Produces<string>(200, "text/xml")))
    .AddWebSocket<TelemetryHandler>("telemetry"));
```

- Marker dedup — repeat calls don't double-register the provider, but the configure callback always runs (so additional `AddWebSocket<T>` bindings can be chained from multiple call sites if needed)
- Binds instances from `Cirreum:Invocation:Providers:WebSocket:Instances:*`
- Registers the L3 `WebSocketInvocationRegistrar`, which in turn:
  - Wires `WebSocketOrchestrator` (the per-connection driver)
  - Registers `IConnectionSender` → `WebSocketConnectionSender` (scoped)
  - Registers `IWebSocketUrlBuilder` → `WebSocketUrlBuilder` (singleton, instance-aware via endpoint metadata)
  - Binds `IOptions<WebSocketInvocationSettings>` for the URL builder

### `AddWebSocket<THandler>(builder, instanceKey, request?)` — per-instance handler binding

```csharp
public static IInvocationBuilder AddWebSocket<THandler>(
    this IInvocationBuilder builder,
    string instanceKey,
    Action<IWebSocketRequestBuilder>? request = null) where THandler : WebSocketHandler;
```

- Stashes a `WebSocketHandlerMapping` in DI for the L3 registrar's `MapSource` to pick up at endpoints-phase time
- Registers `THandler` as scoped — one instance per connection
- **Handler-type uniqueness check** — same rationale as SignalR's Hub-type uniqueness: each handler maps to exactly one instance. Apps that want the same handler logic at multiple paths subclass and map the subclasses
- The `request:` builder is the framework's slim minimal API surface for the companion request endpoint — see below
- Mismatched configuration is caught at startup: `RequestPath` configured but no `request:` builder (or vice versa) throws with a clear, actionable message naming the instance key

### `IWebSocketRequestBuilder` — slim minimal API surface for the request endpoint

```csharp
public interface IWebSocketRequestBuilder {
    void Map(Delegate handler, Action<RouteHandlerBuilder>? configure = null);
    void Map(string httpMethod, Delegate handler, Action<RouteHandlerBuilder>? configure = null);
}
```

| Overload | Default | Use |
|---|---|---|
| `Map(Delegate, configure?)` | POST | Webhook-style endpoints (Twilio, Stripe, GitHub). |
| `Map(string method, Delegate, configure?)` | (explicit) | GET-style negotiation or other rare methods. |

- The `Delegate` parameter accepts inline lambdas, static method groups, or instance method groups — full minimal API parameter binding applies (DI services, `HttpContext`, route parameters, `IResult` return types)
- The optional `configure` callback hooks into the real `RouteHandlerBuilder` at endpoints-phase time, so apps can chain `WithName`, `WithTags`, `Produces<T>`, `WithOpenApi`, `RequireAuthorization`, etc.

### `MapWebSocketInvocation(endpoints)` — endpoints-phase mapping

```csharp
var app = builder.Build();
app.MapWebSocketInvocation();
app.Run();
```

- Calls `UseWebSockets()` automatically — apps don't have to remember it
- Iterates registered `InvocationProviderMapping` services, filters by `WebSocketInvocationRegistrar.ProviderKey`, invokes each registrar's `Map` closure
- For each enabled instance:
  - Maps the WebSocket endpoint at `Path` (always), excluded from OpenAPI/Swagger discovery
  - Maps the request HTTP endpoint at `RequestPath` (when configured + handler provided), with `WebSocketInstanceMetadata` attached so `IWebSocketUrlBuilder` can resolve the instance implicitly
  - Applies `RequireAuthorization` to both endpoints when `Scheme` is configured

Apps using multiple invocation sources may prefer the umbrella `MapInvocation()` from `Cirreum.Runtime.Invocation` (forthcoming) which invokes every registered mapping regardless of source.

---

## Quick-start example

The Twilio media-stream pattern from the IVA reference codebase, ported to use this package:

**Configuration:**
```json
{
  "Cirreum": {
    "Invocation": {
      "Providers": {
        "WebSocket": {
          "Instances": {
            "media": {
              "Enabled": true,
              "Path": "/twilio/media-stream/{callSid}",
              "RequestPath": "/twilio/incoming-call"
            }
          }
        }
      }
    }
  }
}
```

**Program.cs:**
```csharp
builder.AddWebSocketInvocation(b => b
    .AddWebSocket<TwilioMediaHandler>("media", request: r => r
        .Map(TwilioApi.HandleRequest, m => m
            .WithName("twilio-incoming-call")
            .WithTags("twilio")
            .Produces<string>(200, "text/xml"))));

var app = builder.Build();
app.MapWebSocketInvocation();
app.Run();
```

**Request delegate (regular minimal API endpoint):**
```csharp
public static IResult HandleRequest(
    HttpContext context,
    IWebSocketUrlBuilder urls,
    ITwilioRequestValidator validator,
    DomainApiClient domainApi) {

    if (!validator.ValidateRequest(context)) return Results.Unauthorized();

    var callSid = context.Request.Form["CallSid"].ToString();
    var session = await domainApi.ClaimSessionAsync(callSid);
    if (!session.IsSuccess) return Results.Content(HangupTwiml(), "text/xml");

    var streamUrl = urls.Build(context);   // {callSid} auto-extracted from form

    return Results.Content($"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Response>
            <Connect><Stream url="{streamUrl}" /></Connect>
        </Response>
        """, "text/xml");
}
```

**Handler:**
```csharp
public sealed class TwilioMediaHandler : WebSocketHandler {

    public override async Task OnConnectedAsync(CancellationToken ct) {
        // Open outbound AI socket; spawn its read loop.
        // When the AI side ends, call Connection!.Abort() to terminate inbound.
    }

    public override async Task OnMessageAsync(
        IInvocationContext context,
        ReadOnlyMemory<byte> message,
        WebSocketMessageType messageType) {
        // Forward Twilio frames to AI. context.Aborted = connection cancellation.
    }

    public override async Task OnDisconnectedAsync(DisconnectInfo info, CancellationToken ct) {
        // ct is a bounded cleanup budget (30s or host shutdown). On exhaustion, bail.
        if (info.Exception is not null) {
            _logger.LogError(info.Exception, "Call ended with error");
        }
        // Close the outbound AI socket; complete the call record.
    }
}
```

---

## Coordinated downstream work

This release ships in lockstep with:

- **`Cirreum.Invocation.WebSockets 1.0.0`** — the L3 transport adapter this package wraps
- **`Cirreum.InvocationProvider 1.2.0`** — the upstream contract both layers implement (`IInvocationConnection.Abort()`)

---

## Compatibility

- Built against `Cirreum.Runtime.InvocationProvider 1.1.0` and `Cirreum.Invocation.WebSockets 1.0.0`
- Targets `net10.0` / `Microsoft.AspNetCore.App`
- No prior versions — initial release

---

## See also

- `CHANGELOG.md` — condensed change list for 1.0.0.
- [`Cirreum.Invocation.WebSockets`](https://www.nuget.org/packages/Cirreum.Invocation.WebSockets) — the L3 transport adapter this package depends on.
- [`Cirreum.Runtime.Invocation.SignalR`](https://www.nuget.org/packages/Cirreum.Runtime.Invocation.SignalR) — peer L5 Runtime Extensions package; same registration pattern for SignalR.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — the foundational design decision for the unified Invocation seam.
