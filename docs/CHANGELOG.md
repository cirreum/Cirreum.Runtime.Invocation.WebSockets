# Cirreum.Runtime.Invocation.WebSockets Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `AddWebSocketInvocation()` — app-facing extension for registering the WebSocket invocation source
- `AddWebSocket<THandler>(instanceKey, request?)` — binds handler types to configured instances; optional `request:` builder configures the companion HTTP endpoint that initiates the WebSocket flow
- `IWebSocketRequestBuilder` — slim minimal API surface for the request endpoint with `Map(handler, configure?)` (defaults to POST) and `Map(httpMethod, handler, configure?)` (explicit method) overloads. The `configure` callback hooks into the real `RouteHandlerBuilder` for OpenAPI / naming / tags / additional metadata
- `MapWebSocketInvocation()` — endpoint-mapping extension for wiring WebSocket endpoints; calls `UseWebSockets()` automatically and excludes WebSocket endpoints from OpenAPI/Swagger discovery
