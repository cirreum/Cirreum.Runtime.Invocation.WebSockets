namespace Microsoft.Extensions.Hosting;

using Cirreum.Invocation;
using Cirreum.Invocation.Configuration;
using Cirreum.Invocation.WebSockets;
using Cirreum.Providers;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// App-facing extensions for registering the Cirreum WebSocket invocation source.
/// </summary>
public static class HostApplicationBuilderExtensions {

	private sealed class AddWebSocketInvocationMarker { }

	/// <summary>
	/// Registers the Cirreum WebSocket invocation source — surfaces raw WebSocket endpoints
	/// as Cirreum invocation sources, with full per-message <see cref="IInvocationContext"/>
	/// publication, per-connection <c>IInvocationConnection</c> materialization,
	/// <c>IConnectionLifecycle</c> callback dispatch, and <c>IConnectionSender</c> server-push.
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

			builder.RegisterInvocationProvider<
				WebSocketInvocationRegistrar,
				WebSocketInvocationSettings,
				WebSocketInvocationInstanceSettings>();
		}

		configure?.Invoke(new InvocationBuilder(builder));
		return builder;
	}

}
