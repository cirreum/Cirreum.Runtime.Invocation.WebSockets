namespace Microsoft.AspNetCore.Builder;

using Cirreum.Invocation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// App-facing extensions for mapping the Cirreum WebSocket invocation-source endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions {

	/// <summary>
	/// Maps all enabled Cirreum WebSocket invocation instances declared in configuration.
	/// Automatically calls <c>UseWebSockets()</c> on the application pipeline and invokes
	/// each registered <see cref="InvocationProviderMapping"/> whose
	/// <see cref="InvocationProviderMapping.ProviderName"/> matches
	/// <see cref="WebSocketInvocationRegistrar.ProviderKey"/>. For each instance, wires the
	/// WebSocket endpoint at <c>Path</c> and, when configured, the companion HTTP upgrade
	/// endpoint at <c>UpgradePath</c>. Applies <c>RequireAuthorization</c> when a
	/// <c>Scheme</c> is configured.
	/// </summary>
	/// <param name="endpoints">The endpoint route builder.</param>
	/// <returns>The endpoint route builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Apps using multiple invocation sources may prefer the umbrella
	/// <c>MapInvocation()</c> from <c>Cirreum.Runtime.Invocation</c> which invokes every
	/// registered mapping regardless of source. When using
	/// <c>MapWebSocketInvocation()</c> directly, apps do not need to call
	/// <c>app.UseWebSockets()</c> separately — this method handles it.
	/// </para>
	/// </remarks>
	public static IEndpointRouteBuilder MapWebSocketInvocation(
		this IEndpointRouteBuilder endpoints) {

		// Wire up the WebSocket middleware so apps don't have to remember to call UseWebSockets().
		if (endpoints is IApplicationBuilder app) {
			app.UseWebSockets();
		}

		var mappings = endpoints.ServiceProvider.GetServices<InvocationProviderMapping>();
		foreach (var mapping in mappings) {
			if (mapping.ProviderName == WebSocketInvocationRegistrar.ProviderKey) {
				mapping.Map(endpoints);
			}
		}

		return endpoints;
	}

}
