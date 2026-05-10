# Cirreum.Runtime.Invocation.WebSockets Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Provider-level `WebSocketOptions` configuration** — `AddWebSocketInvocation()` now binds `Cirreum:Invocation:Providers:WebSocket:WebSocketOptions` to ASP.NET's `IOptions<WebSocketOptions>` (using `services.Configure<WebSocketOptions>(section)`). The WebSocket middleware reads from DI at request time, so `MapWebSocketInvocation()`'s call to `app.UseWebSockets()` automatically picks up these values — apps no longer need to call `app.UseWebSockets(options)` themselves before `MapWebSocketInvocation()`. Set provider-level `KeepAliveInterval`, `KeepAliveTimeout`, and `AllowedOrigins` directly in configuration:
  ```json
  "Cirreum:Invocation:Providers:WebSocket:WebSocketOptions": {
    "KeepAliveInterval": "00:02:00",
    "KeepAliveTimeout":  "00:00:30",
    "AllowedOrigins":    [ "https://app.example.com" ]
  }
  ```
  Per-instance `KeepAliveInterval` / `KeepAliveTimeout` overrides on `WebSocketInvocationInstanceSettings` still take precedence at `AcceptWebSocketAsync` time for that specific connection. Mirrors the SignalR L5's `Cirreum:Invocation:Providers:SignalR:HubOptions` global-defaults pattern. Apps that need to set `WebSocketOptions` programmatically (callback-based) can still do so via `services.Configure<WebSocketOptions>(o => ...)` — the options system merges configuration sources naturally. On `AllowedOrigins`: this is a defense against browser-based CSWSH (Cross-Site WebSocket Hijacking) when the auth model relies on auto-attached browser credentials (cookies). Cirreum's API-first, stateless, sessionless, cookieless design eliminates this attack surface — set it only if the app mixes cookie auth and WebSocket.

### Changed

- Bumped `Cirreum.Invocation.WebSockets` dependency to flow through the L2 `IConnectionSender` → `IInvocationConnection.SendAsync` consolidation, the new public `IWebSocketConnection` interface, and the connection-captured `SerializerOptions` (see the upcoming `Cirreum.InvocationProvider` and `Cirreum.Invocation.WebSockets` release notes). No changes to the L5 surface — `AddWebSocketInvocation` / `AddWebSocket<THandler>` / `MapWebSocketInvocation` are unchanged. App-side migrations:
  - Cross-cutting code injecting `IConnectionSender` → `accessor.Current?.Connection?.SendAsync(...)`.
  - Handler code calling `this.SendAsync(bytes, WebSocketMessageType.Binary, ct)` → `this.Connection.SendBytesAsync(bytes, WebSocketMessageType.Binary, ct)` (no cast needed — `WebSocketHandler.Connection` is now typed as `IWebSocketConnection`).

## [1.0.0] - 2026-05-09

### Added

- `AddWebSocketInvocation()` — app-facing extension for registering the WebSocket invocation source
- `AddWebSocket<THandler>(instanceKey, request?)` — binds handler types to configured instances; optional `request:` builder configures the companion HTTP endpoint that initiates the WebSocket flow
- `IWebSocketRequestBuilder` — slim minimal API surface for the request endpoint with `Map(handler, configure?)` (defaults to POST) and `Map(httpMethod, handler, configure?)` (explicit method) overloads. The `configure` callback hooks into the real `RouteHandlerBuilder` for OpenAPI / naming / tags / additional metadata
- `MapWebSocketInvocation()` — endpoint-mapping extension for wiring WebSocket endpoints; calls `UseWebSockets()` automatically and excludes WebSocket endpoints from OpenAPI/Swagger discovery
