# Cirreum.Runtime.Invocation.WebSockets 1.1.0 — `IConnectionSender` consolidation + `IWebSocketConnection` + provider-level `WebSocketOptions`

Two-fold release:

1. **Flow-through bump** for the L2 `IConnectionSender` → `IInvocationConnection.SendAsync` consolidation, the new public `IWebSocketConnection` interface, and the connection-captured `SerializerOptions` (via `Cirreum.Invocation.WebSockets 1.2.0`).
2. **Provider-level `WebSocketOptions` configuration** — set ASP.NET WebSocket middleware defaults (`KeepAliveInterval`, `KeepAliveTimeout`, `AllowedOrigins`) directly in configuration; `MapWebSocketInvocation()` picks them up automatically. Apps no longer need to call `app.UseWebSockets(options)` themselves before `MapWebSocketInvocation()`.

The L5 method-shape surface is unchanged: `AddWebSocketInvocation` / `AddWebSocket<THandler>` / `MapWebSocketInvocation` are byte-compatible with 1.0.0.

---

## What's new

### Provider-level `WebSocketOptions` configuration

`AddWebSocketInvocation()` now binds `Cirreum:Invocation:Providers:WebSocket:WebSocketOptions` to ASP.NET's `IOptions<WebSocketOptions>` via `services.Configure<WebSocketOptions>(section)`. The WebSocket middleware reads from DI at request time, so `MapWebSocketInvocation()`'s call to `app.UseWebSockets()` automatically picks up these values.

```json
"Cirreum": {
  "Invocation": {
    "Providers": {
      "WebSocket": {

        "WebSocketOptions": {
          "KeepAliveInterval": "00:02:00",
          "KeepAliveTimeout":  "00:00:30",
          "AllowedOrigins":    [ "https://app.example.com" ]
        },

        "Instances": { /* ... */ }
      }
    }
  }
}
```

| Field | Default | Purpose |
|---|---|---|
| `KeepAliveInterval` | 2 minutes | Protocol-level ping interval. |
| `KeepAliveTimeout` | 30 seconds | Abort the connection if no pong within this time. |
| `AllowedOrigins` | (empty — no filtering) | Origin header allowlist. **Low-priority for Cirreum apps** — see CSWSH note below. |

Per-instance `KeepAliveInterval` / `KeepAliveTimeout` overrides on `WebSocketInvocationInstanceSettings` still take precedence at `AcceptWebSocketAsync` time for that specific connection.

This mirrors the SignalR L5's `Cirreum:Invocation:Providers:SignalR:HubOptions` global-defaults pattern — sub-section binding (vs whole-section binding) keeps the Cirreum framework structure (Instances dictionary, etc.) on the provider section root strictly separated from the WebSocket-native options surface.

Apps that need to set `WebSocketOptions` programmatically (callback-based) can still do so via `services.Configure<WebSocketOptions>(o => ...)` — the options system merges configuration sources naturally.

**On `AllowedOrigins` specifically:** the Origin header is a defense against browser-based CSWSH (Cross-Site WebSocket Hijacking) when the auth model relies on automatically-attached browser credentials (cookies). Cirreum's API-first, stateless, sessionless, cookieless design eliminates this attack surface — token-bearer auth (header or query string) doesn't get auto-attached cross-origin, so a malicious site's JavaScript can't ride a victim's authenticated session. Set this only if the app mixes cookie auth and WebSocket.

---

## What flowed through from L2/L3

- **`Cirreum.Invocation.WebSockets` 1.1.0 → 1.2.0** — pulls in:
  - `WebSocketConnection.SendAsync<T>` (uses captured `JsonSerializerOptions` from the handler).
  - New public `IWebSocketConnection` interface with `SendBytesAsync` for raw frame writes.
  - `WebSocketHandler.Connection` typed as `IWebSocketConnection` (non-nullable, sentinel-backed pre-establishment).
  - Connection-captured `SerializerOptions` — cross-cutting code now picks up the handler's source-gen JSON automatically.
  - Consolidation of `WebSocketConnectionSender` and the handler raw-bytes overload into `IInvocationConnection.SendAsync<T>` / `IWebSocketConnection.SendBytesAsync`.

  See [`Cirreum.Invocation.WebSockets 1.2.0` release notes](https://www.nuget.org/packages/Cirreum.Invocation.WebSockets).
- **`Cirreum.InvocationProvider`** flows in transitively at 1.3.0. See [`Cirreum.InvocationProvider 1.3.0` release notes](https://www.nuget.org/packages/Cirreum.InvocationProvider).

---

## App-side migrations

**Cross-cutting code injecting `IConnectionSender`** — switch to the ambient connection:

```diff
- public sealed class NotifyHandler(IInvocationContextAccessor accessor, IConnectionSender sender) {
+ public sealed class NotifyHandler(IInvocationContextAccessor accessor) {

      public async ValueTask Handle(...) {
-         await sender.SendAsync("Notification", payload, ct);
+         await accessor.Current?.Connection?.SendAsync("Notification", payload, ct);
      }
  }
```

**Handler code calling the raw-bytes overload** — call directly on `Connection` (no cast needed):

```diff
  public override async Task OnMessageAsync(IInvocationContext ctx, ReadOnlyMemory<byte> msg, WebSocketMessageType type) {
-     await this.SendAsync(audioChunk, WebSocketMessageType.Binary, ctx.Aborted);
+     await this.Connection.SendBytesAsync(audioChunk, WebSocketMessageType.Binary, ctx.Aborted);
  }
```

`WebSocketHandler.Connection` is now typed as `IWebSocketConnection` (was `IInvocationConnection?`), so binary-frame writes happen straight on the property — no cast, no helper alias, no null-forgiving operators.

**Handler code calling typed `this.SendAsync(payload, ct)` / `this.SendAsync(method, payload, ct)`** — no migration needed; those overloads still exist as shortcuts (now thin forwarders to `Connection.SendAsync`).

---

## Compatibility

- **Source- and binary-compatible** for `AddWebSocketInvocation` / `AddWebSocket<THandler>` / `MapWebSocketInvocation` consumers.
- **Source-incompatible** transitively for app code injecting `IConnectionSender` (see first migration above).
- **Source-incompatible** transitively for handler code using the raw-bytes `this.SendAsync(bytes, ...)` overload (see second migration above).

---

## See also

- `CHANGELOG.md` — condensed change list for `1.1.0`.
- [`Cirreum.InvocationProvider 1.3.0`](https://www.nuget.org/packages/Cirreum.InvocationProvider) — L2 consolidation.
- [`Cirreum.Invocation.WebSockets 1.2.0`](https://www.nuget.org/packages/Cirreum.Invocation.WebSockets) — L3 adapter update + new `IWebSocketConnection` interface.
