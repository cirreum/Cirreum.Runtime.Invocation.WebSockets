namespace Cirreum.Invocation;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// Default <see cref="IWebSocketRequestBuilder"/> implementation. Captures the handler,
/// HTTP method, and configure callback at the <c>AddWebSocket</c> call site (services-
/// phase). The L3 <c>WebSocketInvocationRegistrar.MapSource</c> reads the captured
/// values from <c>WebSocketHandlerMapping</c> and applies them to the real route at
/// endpoints-phase time.
/// </summary>
internal sealed class WebSocketRequestBuilder : IWebSocketRequestBuilder {

	internal Delegate? Handler { get; private set; }

	internal string? HttpMethod { get; private set; }

	internal Action<RouteHandlerBuilder>? Configurator { get; private set; }

	public void Map(Delegate handler, Action<RouteHandlerBuilder>? configure = null) =>
		this.Capture(httpMethod: null, handler, configure);

	public void Map(string httpMethod, Delegate handler, Action<RouteHandlerBuilder>? configure = null) {
		ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
		this.Capture(httpMethod, handler, configure);
	}

	private void Capture(string? httpMethod, Delegate handler, Action<RouteHandlerBuilder>? configure) {
		ArgumentNullException.ThrowIfNull(handler);

		if (this.Handler is not null) {
			throw new InvalidOperationException(
				"IWebSocketRequestBuilder.Map(...) was called more than once. " +
				"Only one Map call is supported per AddWebSocket invocation.");
		}

		this.Handler = handler;
		this.HttpMethod = httpMethod;
		this.Configurator = configure;
	}

}
