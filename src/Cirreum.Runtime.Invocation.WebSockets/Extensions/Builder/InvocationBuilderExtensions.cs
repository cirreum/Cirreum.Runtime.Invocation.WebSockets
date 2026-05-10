namespace Cirreum.Invocation;

using Cirreum.Invocation.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// App-facing extensions on <see cref="IInvocationBuilder"/> for binding WebSocket handlers
/// to configured invocation-source instances.
/// </summary>
public static class InvocationBuilderExtensions {

	/// <summary>
	/// Binds <typeparamref name="THandler"/> to the configured WebSocket invocation-source
	/// instance identified by <paramref name="instanceKey"/>. The handler type and optional
	/// request-endpoint configuration are captured and stashed in DI as a
	/// <see cref="WebSocketHandlerMapping"/>; the L3 <c>WebSocketInvocationRegistrar.MapSource</c>
	/// resolves these mappings at endpoints-phase time and wires the WebSocket endpoint
	/// and the optional request endpoint for each instance.
	/// </summary>
	/// <typeparam name="THandler">The concrete <see cref="WebSocketHandler"/> type to map.</typeparam>
	/// <param name="builder">The invocation builder (created by <c>AddWebSocketInvocation</c>).</param>
	/// <param name="instanceKey">
	/// The WebSocket instance key — must match a configured instance under
	/// <c>Cirreum:Invocation:Providers:WebSocket:Instances:{instanceKey}</c>.
	/// </param>
	/// <param name="request">
	/// Optional builder for the companion HTTP request endpoint that initiates the WebSocket
	/// flow (e.g. Twilio's incoming-call webhook). Inside the callback, call <c>Map(handler)</c>
	/// (defaults to POST) or <c>Map("METHOD", handler)</c> for an explicit method, with an
	/// optional <c>configure</c> callback for OpenAPI / naming / tags / additional metadata.
	/// Required when the configured instance has a <c>RequestPath</c>; the framework throws
	/// at startup if either is set without the other.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// // Simple — no request endpoint:
	/// builder.AddWebSocketInvocation(b =&gt; b
	///     .AddWebSocket&lt;TelemetryHandler&gt;("telemetry"));
	///
	/// // Two-phase — Twilio webhook with OpenAPI metadata:
	/// builder.AddWebSocketInvocation(b =&gt; b
	///     .AddWebSocket&lt;TwilioMediaHandler&gt;("media", request: r =&gt; r
	///         .Map(TwilioApi.HandleRequest, m =&gt; m
	///             .WithName("twilio-incoming-call")
	///             .WithTags("twilio")
	///             .Produces&lt;string&gt;(200, "text/xml"))));
	///
	/// // Explicit method:
	/// builder.AddWebSocketInvocation(b =&gt; b
	///     .AddWebSocket&lt;H&gt;("k", request: r =&gt; r.Map("GET", GetHandler)));
	/// </code>
	/// </example>
	public static IInvocationBuilder AddWebSocket<THandler>(
		this IInvocationBuilder builder,
		string instanceKey,
		Action<IWebSocketRequestBuilder>? request = null
	) where THandler : WebSocketHandler {

		ArgumentException.ThrowIfNullOrWhiteSpace(instanceKey);

		var services = builder.HostBuilder.Services;

		// Validate THandler uniqueness across this host — same rationale as SignalR's
		// Hub-type uniqueness check. Each handler type maps to exactly one instance.
		var existing = services.FirstOrDefault(d =>
			d.ServiceType == typeof(WebSocketHandlerMapping)
			&& d.ImplementationInstance is WebSocketHandlerMapping m
			&& m.HandlerType == typeof(THandler));

		if (existing is not null) {
			var existingKey = ((WebSocketHandlerMapping)existing.ImplementationInstance!).InstanceKey;
			throw new InvalidOperationException(
				$"WebSocket handler type '{typeof(THandler).Name}' is already mapped to instance '{existingKey}'. " +
				$"Each handler type can be mapped exactly once. To expose the same handler at multiple paths, " +
				$"subclass it and map the subclasses to separate instance keys.");
		}

		// Resolve the request builder (if provided) — capture handler, method, configurator.
		Delegate? requestHandler = null;
		string? requestMethod = null;
		Action<Microsoft.AspNetCore.Builder.RouteHandlerBuilder>? configureRequestRoute = null;

		if (request is not null) {
			var requestBuilder = new WebSocketRequestBuilder();
			request(requestBuilder);

			if (requestBuilder.Handler is null) {
				throw new InvalidOperationException(
					$"AddWebSocket<{typeof(THandler).Name}>(\"{instanceKey}\", request: ...) was called " +
					$"but the request builder never invoked Map(...). Call .Map(handler) inside the " +
					$"request builder, or remove the request: argument entirely.");
			}

			requestHandler = requestBuilder.Handler;
			requestMethod = requestBuilder.HttpMethod;
			configureRequestRoute = requestBuilder.Configurator;
		}

		services.AddSingleton(
			new WebSocketHandlerMapping(
				instanceKey,
				typeof(THandler),
				requestHandler,
				requestMethod,
				configureRequestRoute));

		// Register the handler as a scoped service — one instance per connection.
		services.TryAddScoped<THandler>();

		return builder;
	}

}
