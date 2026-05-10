namespace Cirreum.Invocation;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// Slim builder for the optional companion HTTP <c>Request</c> endpoint that initiates
/// a WebSocket flow (Twilio incoming-call webhook, session-creation endpoint, token
/// issuance, etc.). Passed to apps via the <c>request:</c> parameter on
/// <c>AddWebSocket&lt;THandler&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two overloads of <see cref="Map(Delegate, Action{RouteHandlerBuilder}?)"/>:
/// the parameterless-method overload defaults to <c>POST</c> (the natural webhook
/// method); the explicit-method overload accepts a single HTTP method (<c>"GET"</c>,
/// <c>"PUT"</c>, etc.) for the rare cases where POST isn't right.
/// </para>
/// <para>
/// The optional <c>configure</c> callback hooks into the real
/// <see cref="RouteHandlerBuilder"/> at endpoints-phase time, so apps can chain
/// <c>WithName()</c>, <c>WithTags()</c>, <c>Produces&lt;T&gt;()</c>, <c>WithOpenApi()</c>,
/// <c>RequireAuthorization()</c>, and any other minimal API extension.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Default POST + OpenAPI metadata:
/// .AddWebSocket&lt;TwilioMediaHandler&gt;("media", request: r =&gt; r
///     .Map(TwilioApi.HandleRequest, m =&gt; m
///         .WithName("twilio-incoming-call")
///         .WithTags("twilio")
///         .Produces&lt;string&gt;(200, "text/xml")))
///
/// // Explicit GET (rare):
/// .AddWebSocket&lt;H&gt;("k", request: r =&gt; r.Map("GET", GetHandler))
///
/// // Simple — just POST, no extra config:
/// .AddWebSocket&lt;H&gt;("k", request: r =&gt; r.Map(HandleRequest))
/// </code>
/// </example>
public interface IWebSocketRequestBuilder {

	/// <summary>
	/// Map the request endpoint with the default <c>POST</c> method.
	/// </summary>
	/// <param name="handler">The minimal API delegate for the request endpoint.</param>
	/// <param name="configure">
	/// Optional callback to customize the route's <see cref="RouteHandlerBuilder"/>
	/// (OpenAPI, naming, tags, additional metadata).
	/// </param>
	void Map(Delegate handler, Action<RouteHandlerBuilder>? configure = null);

	/// <summary>
	/// Map the request endpoint with an explicit HTTP method.
	/// </summary>
	/// <param name="httpMethod">The HTTP method (e.g. <c>"GET"</c>, <c>"PUT"</c>).</param>
	/// <param name="handler">The minimal API delegate for the request endpoint.</param>
	/// <param name="configure">
	/// Optional callback to customize the route's <see cref="RouteHandlerBuilder"/>
	/// (OpenAPI, naming, tags, additional metadata).
	/// </param>
	void Map(string httpMethod, Delegate handler, Action<RouteHandlerBuilder>? configure = null);

}
