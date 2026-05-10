namespace Microsoft.Extensions.Hosting;

using Cirreum.Invocation;
using Cirreum.Invocation.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// App-facing extensions for registering the Cirreum WebSocket invocation source.
/// </summary>
public static class HostApplicationBuilderExtensions {

	private sealed class AddWebSocketInvocationMarker { }

	/// <summary>
	/// Registers the Cirreum WebSocket invocation source — surfaces raw WebSocket endpoints
	/// as Cirreum invocation sources, with full per-message <see cref="IInvocationContext"/>
	/// publication, per-connection <c>IWebSocketConnection</c> materialization,
	/// <c>IConnectionLifecycle</c> callback dispatch, and typed-payload server-push through
	/// <c>IInvocationConnection.SendAsync</c> (or raw-frame writes through
	/// <c>IWebSocketConnection.SendBytesAsync</c>).
	/// Binds instances from <c>Cirreum:Invocation:Providers:WebSocket:Instances:*</c>.
	/// </summary>
	/// <param name="builder">The host application builder.</param>
	/// <param name="configure">
	/// Optional callback to register per-instance handler bindings using the fluent
	/// <see cref="InvocationBuilderExtensions.AddWebSocket{THandler}(IInvocationBuilder, string, Action{IWebSocketRequestBuilder})"/>
	/// extension method on <see cref="IInvocationBuilder"/>.
	/// </param>
	/// <returns>The host application builder for chaining.</returns>
	/// <example>
	/// <code>
	/// builder.AddWebSocketInvocation(b =&gt; b
	///     .AddWebSocket&lt;VoiceFrameHandler&gt;("voice")
	///     .AddWebSocket&lt;TelemetryHandler&gt;("telemetry"));
	/// </code>
	/// </example>
	public static IHostApplicationBuilder AddWebSocketInvocation(
		this IHostApplicationBuilder builder,
		Action<IInvocationBuilder>? configure = null) {

		if (!builder.Services.IsMarkerTypeRegistered<AddWebSocketInvocationMarker>()) {
			builder.Services.MarkTypeAsRegistered<AddWebSocketInvocationMarker>();

			// Bind global WebSocketOptions defaults from the provider's WebSocketOptions
			// sub-section. Properties declared at
			// Cirreum:Invocation:Providers:WebSocket:WebSocketOptions (KeepAliveInterval,
			// KeepAliveTimeout, AllowedOrigins) apply to the WebSocket middleware
			// process-wide. The middleware reads IOptions<WebSocketOptions> from DI at
			// request time, so MapWebSocketInvocation()'s call to UseWebSockets() (no
			// args) automatically picks up these values. Per-instance KeepAliveInterval /
			// KeepAliveTimeout overrides on WebSocketInvocationInstanceSettings still
			// take precedence at AcceptWebSocketAsync time for that specific connection.
			// Sub-section binding (vs whole-section binding) keeps the Cirreum framework
			// structure (Instances dictionary, etc.) on the provider section root strictly
			// separated from the WebSocket-native options surface — same shape as the
			// SignalR provider's HubOptions sub-section.
			var providerWebSocketOptionsSection = builder.Configuration.GetSection(
				$"Cirreum:Invocation:Providers:{WebSocketInvocationRegistrar.ProviderKey}:WebSocketOptions");
			builder.Services.Configure<WebSocketOptions>(providerWebSocketOptionsSection);

			builder.RegisterInvocationProvider<
				WebSocketInvocationRegistrar,
				WebSocketInvocationSettings,
				WebSocketInvocationInstanceSettings>();
		}

		configure?.Invoke(new InvocationBuilder(builder));
		return builder;
	}

}
