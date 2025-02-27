﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlerFilters;

namespace OpenIddict.Server
{
    public static partial class OpenIddictServerHandlers
    {
        public static class Authentication
        {
            public static ImmutableArray<OpenIddictServerHandlerDescriptor> DefaultHandlers { get; } = ImmutableArray.Create(
                /*
                 * Authorization request top-level processing:
                 */
                ExtractAuthorizationRequest.Descriptor,
                ValidateAuthorizationRequest.Descriptor,
                HandleAuthorizationRequest.Descriptor,
                ApplyAuthorizationResponse<ProcessChallengeResponseContext>.Descriptor,
                ApplyAuthorizationResponse<ProcessErrorResponseContext>.Descriptor,
                ApplyAuthorizationResponse<ProcessRequestContext>.Descriptor,
                ApplyAuthorizationResponse<ProcessSigninResponseContext>.Descriptor,

                /*
                 * Authorization request validation:
                 */
                ValidateRequestParameter.Descriptor,
                ValidateRequestUriParameter.Descriptor,
                ValidateClientIdParameter.Descriptor,
                ValidateRedirectUriParameter.Descriptor,
                ValidateResponseTypeParameter.Descriptor,
                ValidateResponseModeParameter.Descriptor,
                ValidateNonceParameter.Descriptor,
                ValidatePromptParameter.Descriptor,
                ValidateCodeChallengeParameters.Descriptor,
                ValidateClientId.Descriptor,
                ValidateClientType.Descriptor,
                ValidateClientRedirectUri.Descriptor,
                ValidateScopes.Descriptor,
                ValidateEndpointPermissions.Descriptor,
                ValidateGrantTypePermissions.Descriptor,
                ValidateScopePermissions.Descriptor,

                /*
                 * Authorization response processing:
                 */
                AttachRedirectUri.Descriptor,
                InferResponseMode.Descriptor,
                AttachResponseState.Descriptor);

            /// <summary>
            /// Contains the logic responsible of extracting authorization requests and invoking the corresponding event handlers.
            /// </summary>
            public class ExtractAuthorizationRequest : IOpenIddictServerHandler<ProcessRequestContext>
            {
                private readonly IOpenIddictServerProvider _provider;

                public ExtractAuthorizationRequest([NotNull] IOpenIddictServerProvider provider)
                    => _provider = provider;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>()
                        .UseScopedHandler<ExtractAuthorizationRequest>()
                        .SetOrder(int.MinValue + 100_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ProcessRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    if (context.EndpointType != OpenIddictServerEndpointType.Authorization)
                    {
                        return;
                    }

                    var notification = new ExtractAuthorizationRequestContext(context.Transaction);
                    await _provider.DispatchAsync(notification);

                    if (notification.IsRequestHandled)
                    {
                        context.HandleRequest();
                        return;
                    }

                    else if (notification.IsRequestSkipped)
                    {
                        context.SkipRequest();
                        return;
                    }

                    else if (notification.IsRejected)
                    {
                        context.Reject(
                            error: notification.Error ?? Errors.InvalidRequest,
                            description: notification.ErrorDescription,
                            uri: notification.ErrorUri);
                        return;
                    }

                    if (notification.Request == null)
                    {
                        throw new InvalidOperationException(new StringBuilder()
                            .Append("The authorization request was not correctly extracted. To extract authorization requests, ")
                            .Append("create a class implementing 'IOpenIddictServerHandler<ExtractAuthorizationRequestContext>' ")
                            .AppendLine("and register it using 'services.AddOpenIddict().AddServer().AddEventHandler()'.")
                            .ToString());
                    }

                    context.Logger.LogInformation("The authorization request was successfully extracted: {Request}.", notification.Request);
                }
            }

            /// <summary>
            /// Contains the logic responsible of validating authorization requests and invoking the corresponding event handlers.
            /// </summary>
            public class ValidateAuthorizationRequest : IOpenIddictServerHandler<ProcessRequestContext>
            {
                private readonly IOpenIddictServerProvider _provider;

                public ValidateAuthorizationRequest([NotNull] IOpenIddictServerProvider provider)
                    => _provider = provider;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>()
                        .UseScopedHandler<ValidateAuthorizationRequest>()
                        .SetOrder(ExtractAuthorizationRequest.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ProcessRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    if (context.EndpointType != OpenIddictServerEndpointType.Authorization)
                    {
                        return;
                    }

                    var notification = new ValidateAuthorizationRequestContext(context.Transaction);
                    await _provider.DispatchAsync(notification);

                    if (notification.IsRequestHandled)
                    {
                        context.HandleRequest();
                        return;
                    }

                    else if (notification.IsRequestSkipped)
                    {
                        context.SkipRequest();
                        return;
                    }

                    else if (notification.IsRejected)
                    {
                        context.Reject(
                            error: notification.Error ?? Errors.InvalidRequest,
                            description: notification.ErrorDescription,
                            uri: notification.ErrorUri);
                        return;
                    }

                    if (string.IsNullOrEmpty(notification.RedirectUri))
                    {
                        throw new InvalidOperationException("The request cannot be validated because no client_id was specified.");
                    }

                    // Store the validated redirect_uri as an environment property.
                    context.Transaction.Properties[Properties.ValidatedRedirectUri] = notification.RedirectUri;

                    context.Logger.LogInformation("The authorization request was successfully validated.");
                }
            }

            /// <summary>
            /// Contains the logic responsible of handling authorization requests and invoking the corresponding event handlers.
            /// </summary>
            public class HandleAuthorizationRequest : IOpenIddictServerHandler<ProcessRequestContext>
            {
                private readonly IOpenIddictServerProvider _provider;

                public HandleAuthorizationRequest([NotNull] IOpenIddictServerProvider provider)
                    => _provider = provider;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>()
                        .UseScopedHandler<HandleAuthorizationRequest>()
                        .SetOrder(ValidateAuthorizationRequest.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ProcessRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    if (context.EndpointType != OpenIddictServerEndpointType.Authorization)
                    {
                        return;
                    }

                    var notification = new HandleAuthorizationRequestContext(context.Transaction);
                    await _provider.DispatchAsync(notification);

                    if (notification.IsRequestHandled)
                    {
                        context.HandleRequest();
                        return;
                    }

                    else if (notification.IsRequestSkipped)
                    {
                        context.SkipRequest();
                        return;
                    }

                    else if (notification.IsRejected)
                    {
                        context.Reject(
                            error: notification.Error ?? Errors.InvalidRequest,
                            description: notification.ErrorDescription,
                            uri: notification.ErrorUri);
                        return;
                    }

                    if (notification.Principal != null)
                    {
                        var @event = new ProcessSigninResponseContext(context.Transaction)
                        {
                            Principal = notification.Principal,
                            Response = new OpenIddictResponse()
                        };

                        await _provider.DispatchAsync(@event);

                        if (@event.IsRequestHandled)
                        {
                            context.HandleRequest();
                            return;
                        }

                        else if (@event.IsRequestSkipped)
                        {
                            context.SkipRequest();
                            return;
                        }
                    }

                    throw new InvalidOperationException(new StringBuilder()
                        .Append("The authorization request was not handled. To handle authorization requests, ")
                        .Append("create a class implementing 'IOpenIddictServerHandler<HandleAuthorizationRequestContext>' ")
                        .AppendLine("and register it using 'services.AddOpenIddict().AddServer().AddEventHandler()'.")
                        .Append("Alternatively, enable the pass-through mode to handle them at a later stage.")
                        .ToString());
                }
            }

            /// <summary>
            /// Contains the logic responsible of processing sign-in responses and invoking the corresponding event handlers.
            /// </summary>
            public class ApplyAuthorizationResponse<TContext> : IOpenIddictServerHandler<TContext> where TContext : BaseRequestContext
            {
                private readonly IOpenIddictServerProvider _provider;

                public ApplyAuthorizationResponse([NotNull] IOpenIddictServerProvider provider)
                    => _provider = provider;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<TContext>()
                        .UseScopedHandler<ApplyAuthorizationResponse<TContext>>()
                        .SetOrder(int.MaxValue - 100_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] TContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    if (context.EndpointType != OpenIddictServerEndpointType.Authorization)
                    {
                        return;
                    }

                    var notification = new ApplyAuthorizationResponseContext(context.Transaction);
                    await _provider.DispatchAsync(notification);

                    if (notification.IsRequestHandled)
                    {
                        context.HandleRequest();
                        return;
                    }

                    else if (notification.IsRequestSkipped)
                    {
                        context.SkipRequest();
                        return;
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that specify the unsupported request parameter.
            /// </summary>
            public class ValidateRequestParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateRequestParameter>()
                        .SetOrder(int.MinValue + 100_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // Reject requests using the unsupported request parameter.
                    if (!string.IsNullOrEmpty(context.Request.Request))
                    {
                        context.Logger.LogError("The authorization request was rejected because it contained " +
                                                "an unsupported parameter: {Parameter}.", "request");

                        context.Reject(
                            error: Errors.RequestNotSupported,
                            description: "The 'request' parameter is not supported.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that specify the unsupported request_uri parameter.
            /// </summary>
            public class ValidateRequestUriParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateRedirectUriParameter>()
                        .SetOrder(ValidateRequestParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // Reject requests using the unsupported request_uri parameter.
                    if (!string.IsNullOrEmpty(context.Request.RequestUri))
                    {
                        context.Logger.LogError("The authorization request was rejected because it contained " +
                                                "an unsupported parameter: {Parameter}.", "request_uri");

                        context.Reject(
                            error: Errors.RequestUriNotSupported,
                            description: "The 'request_uri' parameter is not supported.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that lack the mandatory client_id parameter.
            /// </summary>
            public class ValidateClientIdParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateClientIdParameter>()
                        .SetOrder(ValidateRequestUriParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // client_id is a required parameter and MUST cause an error when missing.
                    // See http://openid.net/specs/openid-connect-core-1_0.html#AuthRequest.
                    if (string.IsNullOrEmpty(context.ClientId))
                    {
                        context.Logger.LogError("The authorization request was rejected because " +
                                                "the mandatory 'client_id' parameter was missing.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The mandatory 'client_id' parameter is missing.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that lack the mandatory redirect_uri parameter.
            /// </summary>
            public class ValidateRedirectUriParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateRedirectUriParameter>()
                        .SetOrder(ValidateClientIdParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // While redirect_uri was not mandatory in OAuth 2.0, this parameter
                    // is now declared as REQUIRED and MUST cause an error when missing.
                    // See http://openid.net/specs/openid-connect-core-1_0.html#AuthRequest.
                    // To keep OpenIddict compatible with pure OAuth 2.0 clients, an error
                    // is only returned if the request was made by an OpenID Connect client.
                    if (string.IsNullOrEmpty(context.RedirectUri))
                    {
                        if (context.Request.HasScope(Scopes.OpenId))
                        {
                            context.Logger.LogError("The authorization request was rejected because " +
                                                    "the mandatory 'redirect_uri' parameter was missing.");

                            context.Reject(
                                error: Errors.InvalidRequest,
                                description: "The mandatory 'redirect_uri' parameter is missing.");

                            return default;
                        }

                        return default;
                    }

                    // Note: when specified, redirect_uri MUST be an absolute URI.
                    // See http://tools.ietf.org/html/rfc6749#section-3.1.2
                    // and http://openid.net/specs/openid-connect-core-1_0.html#AuthRequest.
                    //
                    // Note: on Linux/macOS, "/path" URLs are treated as valid absolute file URLs.
                    // To ensure relative redirect_uris are correctly rejected on these platforms,
                    // an additional check using IsWellFormedOriginalString() is made here.
                    // See https://github.com/dotnet/corefx/issues/22098 for more information.
                    if (!Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out Uri uri) || !uri.IsWellFormedOriginalString())
                    {
                        context.Logger.LogError("The authorization request was rejected because the 'redirect_uri' parameter " +
                                                "didn't correspond to a valid absolute URL: {RedirectUri}.", context.RedirectUri);

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The 'redirect_uri' parameter must be a valid absolute URL.");

                        return default;
                    }

                    // Note: when specified, redirect_uri MUST NOT include a fragment component.
                    // See http://tools.ietf.org/html/rfc6749#section-3.1.2
                    // and http://openid.net/specs/openid-connect-core-1_0.html#AuthRequest
                    if (!string.IsNullOrEmpty(uri.Fragment))
                    {
                        context.Logger.LogError("The authorization request was rejected because the 'redirect_uri' " +
                                                "contained a URL fragment: {RedirectUri}.", context.RedirectUri);

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The 'redirect_uri' parameter must not include a fragment.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that specify an invalid response_type parameter.
            /// </summary>
            public class ValidateResponseTypeParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateResponseTypeParameter>()
                        .SetOrder(ValidateRedirectUriParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // Reject requests missing the mandatory response_type parameter.
                    if (string.IsNullOrEmpty(context.Request.ResponseType))
                    {
                        context.Logger.LogError("The authorization request was rejected because " +
                                                "the mandatory 'response_type' parameter was missing.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The mandatory 'response_type' parameter is missing.");

                        return default;
                    }

                    // Reject requests containing the id_token response_type if no openid scope has been received.
                    if (context.Request.HasResponseType(ResponseTypes.IdToken) && !context.Request.HasScope(Scopes.OpenId))
                    {
                        context.Logger.LogError("The authorization request was rejected because the 'openid' scope was missing.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The mandatory 'openid' scope is missing.");

                        return default;
                    }

                    // Reject requests containing the code response_type if the token endpoint has been disabled.
                    if (context.Request.HasResponseType(ResponseTypes.Code) && context.Options.TokenEndpointUris.Count == 0)
                    {
                        context.Logger.LogError("The authorization request was rejected because the authorization code flow was disabled.");

                        context.Reject(
                            error: Errors.UnsupportedResponseType,
                            description: "The specified 'response_type' is not supported by this server.");

                        return default;
                    }

                    // Reject requests that specify an unsupported response_type.
                    if (!context.Request.IsAuthorizationCodeFlow() && !context.Request.IsHybridFlow() && !context.Request.IsImplicitFlow())
                    {
                        context.Logger.LogError("The authorization request was rejected because the '{ResponseType}' " +
                                                "response type is not supported.", context.Request.ResponseType);

                        context.Reject(
                            error: Errors.UnsupportedResponseType,
                            description: "The specified 'response_type' parameter is not supported.");

                        return default;
                    }

                    // Reject code flow authorization requests if the authorization code flow is not enabled.
                    if (context.Request.IsAuthorizationCodeFlow() && !context.Options.GrantTypes.Contains(GrantTypes.AuthorizationCode))
                    {
                        context.Logger.LogError("The authorization request was rejected because " +
                                                "the authorization code flow was not enabled.");

                        context.Reject(
                            error: Errors.UnsupportedResponseType,
                            description: "The specified 'response_type' parameter is not allowed.");

                        return default;
                    }

                    // Reject implicit flow authorization requests if the implicit flow is not enabled.
                    if (context.Request.IsImplicitFlow() && !context.Options.GrantTypes.Contains(GrantTypes.Implicit))
                    {
                        context.Logger.LogError("The authorization request was rejected because the implicit flow was not enabled.");

                        context.Reject(
                            error: Errors.UnsupportedResponseType,
                            description: "The specified 'response_type' parameter is not allowed.");

                        return default;
                    }

                    // Reject hybrid flow authorization requests if the authorization code or the implicit flows are not enabled.
                    if (context.Request.IsHybridFlow() && (!context.Options.GrantTypes.Contains(GrantTypes.AuthorizationCode) ||
                                                           !context.Options.GrantTypes.Contains(GrantTypes.Implicit)))
                    {
                        context.Logger.LogError("The authorization request was rejected because the " +
                                                "authorization code flow or the implicit flow was not enabled.");

                        context.Reject(
                            error: Errors.UnsupportedResponseType,
                            description: "The specified 'response_type' parameter is not allowed.");

                        return default;
                    }

                    // Reject authorization requests that specify scope=offline_access if the refresh token flow is not enabled.
                    if (context.Request.HasScope(Scopes.OfflineAccess) && !context.Options.GrantTypes.Contains(GrantTypes.RefreshToken))
                    {
                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The 'offline_access' scope is not allowed.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that specify an invalid response_mode parameter.
            /// </summary>
            public class ValidateResponseModeParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateResponseModeParameter>()
                        .SetOrder(ValidateResponseTypeParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // response_mode=query (explicit or not) and a response_type containing id_token
                    // or token are not considered as a safe combination and MUST be rejected.
                    // See http://openid.net/specs/oauth-v2-multiple-response-types-1_0.html#Security.
                    if (context.Request.IsQueryResponseMode() && (context.Request.HasResponseType(ResponseTypes.IdToken) ||
                                                                  context.Request.HasResponseType(ResponseTypes.Token)))
                    {
                        context.Logger.LogError("The authorization request was rejected because the 'response_type'/'response_mode' " +
                                                "combination was invalid: {ResponseType} ; {ResponseMode}.",
                                                context.Request.ResponseType, context.Request.ResponseMode);

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The specified 'response_type'/'response_mode' combination is invalid.");

                        return default;
                    }

                    // Reject requests that specify an unsupported response_mode.
                    if (!string.IsNullOrEmpty(context.Request.ResponseMode) && !context.Request.IsFormPostResponseMode() &&
                                                                               !context.Request.IsFragmentResponseMode() &&
                                                                               !context.Request.IsQueryResponseMode())
                    {
                        context.Logger.LogError("The authorization request was rejected because the '{ResponseMode}' " +
                                                "response mode is not supported.", context.Request.ResponseMode);

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The specified 'response_mode' parameter is not supported.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that don't specify a nonce.
            /// </summary>
            public class ValidateNonceParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateNonceParameter>()
                        .SetOrder(ValidateResponseModeParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // Reject OpenID Connect implicit/hybrid requests missing the mandatory nonce parameter.
                    // See http://openid.net/specs/openid-connect-core-1_0.html#AuthRequest,
                    // http://openid.net/specs/openid-connect-implicit-1_0.html#RequestParameters
                    // and http://openid.net/specs/openid-connect-core-1_0.html#HybridIDToken.

                    if (!string.IsNullOrEmpty(context.Request.Nonce) || !context.Request.HasScope(Scopes.OpenId))
                    {
                        return default;
                    }

                    if (context.Request.IsImplicitFlow() || context.Request.IsHybridFlow())
                    {
                        context.Logger.LogError("The authorization request was rejected because the mandatory 'nonce' parameter was missing.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The mandatory 'nonce' parameter is missing.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that don't specify a valid prompt parameter.
            /// </summary>
            public class ValidatePromptParameter : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidatePromptParameter>()
                        .SetOrder(ValidateNonceParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // Reject requests specifying prompt=none with consent/login or select_account.
                    if (context.Request.HasPrompt(Prompts.None) && (context.Request.HasPrompt(Prompts.Consent) ||
                                                                    context.Request.HasPrompt(Prompts.Login) ||
                                                                    context.Request.HasPrompt(Prompts.SelectAccount)))
                    {
                        context.Logger.LogError("The authorization request was rejected because an invalid prompt parameter was specified.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The specified 'prompt' parameter is invalid.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that don't specify valid code challenge parameters.
            /// </summary>
            public class ValidateCodeChallengeParameters : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseSingletonHandler<ValidateCodeChallengeParameters>()
                        .SetOrder(ValidatePromptParameter.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    if (string.IsNullOrEmpty(context.Request.CodeChallenge) &&
                        string.IsNullOrEmpty(context.Request.CodeChallengeMethod))
                    {
                        return default;
                    }

                    // Ensure a code_challenge was specified if a code_challenge_method was used.
                    if (string.IsNullOrEmpty(context.Request.CodeChallenge))
                    {
                        context.Logger.LogError("The authorization request was rejected because the code_challenge was missing.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The 'code_challenge_method' parameter cannot be used without 'code_challenge'.");

                        return default;
                    }

                    // When code_challenge or code_challenge_method is specified, ensure the response_type includes "code".
                    if (!context.Request.HasResponseType(ResponseTypes.Code))
                    {
                        context.Logger.LogError("The authorization request was rejected because the response type " +
                                                "was not compatible with 'code_challenge'/'code_challenge_method'.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The 'code_challenge' and 'code_challenge_method' parameters " +
                                         "can only be used with a response type containing 'code'.");

                        return default;
                    }

                    // Reject authorization requests that contain response_type=token when a code_challenge is specified.
                    if (context.Request.HasResponseType(ResponseTypes.Token))
                    {
                        context.Logger.LogError("The authorization request was rejected because the " +
                                                "specified response type was not compatible with PKCE.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The specified 'response_type' parameter is not allowed when using PKCE.");

                        return default;
                    }

                    // If a code_challenge_method was specified, ensure the algorithm is supported.
                    if (!string.IsNullOrEmpty(context.Request.CodeChallengeMethod) &&
                        !string.Equals(context.Request.CodeChallengeMethod, CodeChallengeMethods.Plain, StringComparison.Ordinal) &&
                        !string.Equals(context.Request.CodeChallengeMethod, CodeChallengeMethods.Sha256, StringComparison.Ordinal))
                    {
                        context.Logger.LogError("The authorization request was rejected because " +
                                                "the specified code challenge method was not supported.");

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The specified code_challenge_method is not supported'.");

                        return default;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that use unregistered scopes.
            /// Note: this handler is not used when the degraded mode is enabled or when scope validation is disabled.
            /// </summary>
            public class ValidateScopes : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                private readonly IOpenIddictScopeManager _scopeManager;

                public ValidateScopes() => throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The core services must be registered when enabling the OpenIddict server feature.")
                    .Append("To register the OpenIddict core services, reference the 'OpenIddict.Core' package ")
                    .AppendLine("and call 'services.AddOpenIddict().AddCore()' from 'ConfigureServices'.")
                    .Append("Alternatively, you can disable the built-in database-based server features by enabling ")
                    .Append("the degraded mode with 'services.AddOpenIddict().AddServer().EnableDegradedMode()'.")
                    .ToString());

                public ValidateScopes([NotNull] IOpenIddictScopeManager scopeManager)
                    => _scopeManager = scopeManager;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .AddFilter<RequireScopeValidationEnabled>()
                        .AddFilter<RequireDegradedModeDisabled>()
                        .UseScopedHandler<ValidateScopes>()
                        .SetOrder(ValidateCodeChallengeParameters.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // If all the specified scopes are registered in the options, avoid making a database lookup.
                    var scopes = context.Request.GetScopes().Except(context.Options.Scopes);
                    if (scopes.Count != 0)
                    {
                        await foreach (var scope in _scopeManager.FindByNamesAsync(scopes.ToImmutableArray()))
                        {
                            scopes = scopes.Remove(await _scopeManager.GetNameAsync(scope));
                        }
                    }

                    // If at least one scope was not recognized, return an error.
                    if (scopes.Count != 0)
                    {
                        context.Logger.LogError("The authentication request was rejected because " +
                                                "invalid scopes were specified: {Scopes}.", scopes);

                        context.Reject(
                            error: Errors.InvalidScope,
                            description: "The specified 'scope' parameter is not valid.");

                        return;
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that use an invalid client_id.
            /// Note: this handler is not used when the degraded mode is enabled.
            /// </summary>
            public class ValidateClientId : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                private readonly IOpenIddictApplicationManager _applicationManager;

                public ValidateClientId() => throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The core services must be registered when enabling the OpenIddict server feature.")
                    .Append("To register the OpenIddict core services, reference the 'OpenIddict.Core' package ")
                    .AppendLine("and call 'services.AddOpenIddict().AddCore()' from 'ConfigureServices'.")
                    .Append("Alternatively, you can disable the built-in database-based server features by enabling ")
                    .Append("the degraded mode with 'services.AddOpenIddict().AddServer().EnableDegradedMode()'.")
                    .ToString());

                public ValidateClientId([NotNull] IOpenIddictApplicationManager applicationManager)
                    => _applicationManager = applicationManager;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .AddFilter<RequireDegradedModeDisabled>()
                        .UseScopedHandler<ValidateClientId>()
                        .SetOrder(ValidateScopes.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    var application = await _applicationManager.FindByClientIdAsync(context.ClientId);
                    if (application == null)
                    {
                        context.Logger.LogError("The authorization request was rejected because the client " +
                                                "application was not found: '{ClientId}'.", context.ClientId);

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The specified 'client_id' parameter is invalid.");

                        return;
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests
            /// that use a response_type incompatible with the client application.
            /// Note: this handler is not used when the degraded mode is enabled.
            /// </summary>
            public class ValidateClientType : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                private readonly IOpenIddictApplicationManager _applicationManager;

                public ValidateClientType() => throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The core services must be registered when enabling the OpenIddict server feature.")
                    .Append("To register the OpenIddict core services, reference the 'OpenIddict.Core' package ")
                    .AppendLine("and call 'services.AddOpenIddict().AddCore()' from 'ConfigureServices'.")
                    .Append("Alternatively, you can disable the built-in database-based server features by enabling ")
                    .Append("the degraded mode with 'services.AddOpenIddict().AddServer().EnableDegradedMode()'.")
                    .ToString());

                public ValidateClientType([NotNull] IOpenIddictApplicationManager applicationManager)
                    => _applicationManager = applicationManager;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .AddFilter<RequireDegradedModeDisabled>()
                        .UseScopedHandler<ValidateClientType>()
                        .SetOrder(ValidateClientId.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    var application = await _applicationManager.FindByClientIdAsync(context.ClientId);
                    if (application == null)
                    {
                        throw new InvalidOperationException("The client application details cannot be found in the database.");
                    }

                    // To prevent downgrade attacks, ensure that authorization requests returning an access token directly
                    // from the authorization endpoint are rejected if the client_id corresponds to a confidential application.
                    // Note: when using the authorization code grant, the ValidateClientSecret handler is responsible of rejecting
                    // the token request if the client_id corresponds to an unauthenticated confidential client.
                    if (context.Request.HasResponseType(ResponseTypes.Token) && await _applicationManager.IsConfidentialAsync(application))
                    {
                        context.Logger.LogError("The authorization request was rejected because the confidential application '{ClientId}' " +
                                                "was not allowed to retrieve an access token from the authorization endpoint.", context.ClientId);

                        context.Reject(
                            error: Errors.UnauthorizedClient,
                            description: "The specified 'response_type' parameter is not valid for this client application.");

                        return;
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests that use an invalid redirect_uri.
            /// Note: this handler is not used when the degraded mode is enabled.
            /// </summary>
            public class ValidateClientRedirectUri : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                private readonly IOpenIddictApplicationManager _applicationManager;

                public ValidateClientRedirectUri() => throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The core services must be registered when enabling the OpenIddict server feature.")
                    .Append("To register the OpenIddict core services, reference the 'OpenIddict.Core' package ")
                    .AppendLine("and call 'services.AddOpenIddict().AddCore()' from 'ConfigureServices'.")
                    .Append("Alternatively, you can disable the built-in database-based server features by enabling ")
                    .Append("the degraded mode with 'services.AddOpenIddict().AddServer().EnableDegradedMode()'.")
                    .ToString());

                public ValidateClientRedirectUri([NotNull] IOpenIddictApplicationManager applicationManager)
                    => _applicationManager = applicationManager;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .AddFilter<RequireDegradedModeDisabled>()
                        .UseScopedHandler<ValidateClientRedirectUri>()
                        .SetOrder(ValidateClientType.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    var application = await _applicationManager.FindByClientIdAsync(context.ClientId);
                    if (application == null)
                    {
                        throw new InvalidOperationException("The client application details cannot be found in the database.");
                    }

                    // Ensure that the specified redirect_uri is valid and is associated with the client application.
                    if (!await _applicationManager.ValidateRedirectUriAsync(application, context.RedirectUri))
                    {
                        context.Logger.LogError("The authorization request was rejected because the redirect_uri " +
                                                "was invalid: '{RedirectUri}'.", context.RedirectUri);

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The specified 'redirect_uri' parameter is not valid for this client application.");

                        return;
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests made by unauthorized applications.
            /// Note: this handler is not used when the degraded mode is enabled or when endpoint permissions are disabled.
            /// </summary>
            public class ValidateEndpointPermissions : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                private readonly IOpenIddictApplicationManager _applicationManager;

                public ValidateEndpointPermissions() => throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The core services must be registered when enabling the OpenIddict server feature.")
                    .Append("To register the OpenIddict core services, reference the 'OpenIddict.Core' package ")
                    .AppendLine("and call 'services.AddOpenIddict().AddCore()' from 'ConfigureServices'.")
                    .Append("Alternatively, you can disable the built-in database-based server features by enabling ")
                    .Append("the degraded mode with 'services.AddOpenIddict().AddServer().EnableDegradedMode()'.")
                    .ToString());

                public ValidateEndpointPermissions([NotNull] IOpenIddictApplicationManager applicationManager)
                    => _applicationManager = applicationManager;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .AddFilter<RequireEndpointPermissionsEnabled>()
                        .AddFilter<RequireDegradedModeDisabled>()
                        .UseScopedHandler<ValidateEndpointPermissions>()
                        .SetOrder(ValidateClientRedirectUri.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    var application = await _applicationManager.FindByClientIdAsync(context.ClientId);
                    if (application == null)
                    {
                        throw new InvalidOperationException("The client application details cannot be found in the database.");
                    }

                    // Reject the request if the application is not allowed to use the authorization endpoint.
                    if (!await _applicationManager.HasPermissionAsync(application, Permissions.Endpoints.Authorization))
                    {
                        context.Logger.LogError("The authorization request was rejected because the application '{ClientId}' " +
                                                "was not allowed to use the authorization endpoint.", context.ClientId);

                        context.Reject(
                            error: Errors.UnauthorizedClient,
                            description: "This client application is not allowed to use the authorization endpoint.");

                        return;
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests made by unauthorized applications.
            /// Note: this handler is not used when the degraded mode is enabled or when grant type permissions are disabled.
            /// </summary>
            public class ValidateGrantTypePermissions : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                private readonly IOpenIddictApplicationManager _applicationManager;

                public ValidateGrantTypePermissions() => throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The core services must be registered when enabling the OpenIddict server feature.")
                    .Append("To register the OpenIddict core services, reference the 'OpenIddict.Core' package ")
                    .AppendLine("and call 'services.AddOpenIddict().AddCore()' from 'ConfigureServices'.")
                    .Append("Alternatively, you can disable the built-in database-based server features by enabling ")
                    .Append("the degraded mode with 'services.AddOpenIddict().AddServer().EnableDegradedMode()'.")
                    .ToString());

                public ValidateGrantTypePermissions([NotNull] IOpenIddictApplicationManager applicationManager)
                    => _applicationManager = applicationManager;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .AddFilter<RequireGrantTypePermissionsEnabled>()
                        .AddFilter<RequireDegradedModeDisabled>()
                        .UseScopedHandler<ValidateGrantTypePermissions>()
                        .SetOrder(ValidateEndpointPermissions.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    var application = await _applicationManager.FindByClientIdAsync(context.ClientId);
                    if (application == null)
                    {
                        throw new InvalidOperationException("The client application details cannot be found in the database.");
                    }

                    // Reject the request if the application is not allowed to use the authorization code flow.
                    if (context.Request.IsAuthorizationCodeFlow() &&
                        !await _applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.AuthorizationCode))
                    {
                        context.Logger.LogError("The authorization request was rejected because the application '{ClientId}' " +
                                                "was not allowed to use the authorization code flow.", context.ClientId);

                        context.Reject(
                            error: Errors.UnauthorizedClient,
                            description: "The client application is not allowed to use the authorization code flow.");

                        return;
                    }

                    // Reject the request if the application is not allowed to use the implicit flow.
                    if (context.Request.IsImplicitFlow() &&
                        !await _applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.Implicit))
                    {
                        context.Logger.LogError("The authorization request was rejected because the application '{ClientId}' " +
                                                "was not allowed to use the implicit flow.", context.ClientId);

                        context.Reject(
                            error: Errors.UnauthorizedClient,
                            description: "The client application is not allowed to use the implicit flow.");

                        return;
                    }

                    // Reject the request if the application is not allowed to use the authorization code/implicit flows.
                    if (context.Request.IsHybridFlow() &&
                       (!await _applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.AuthorizationCode) ||
                        !await _applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.Implicit)))
                    {
                        context.Logger.LogError("The authorization request was rejected because the application '{ClientId}' " +
                                                "was not allowed to use the hybrid flow.", context.ClientId);

                        context.Reject(
                            error: Errors.UnauthorizedClient,
                            description: "The client application is not allowed to use the hybrid flow.");

                        return;
                    }

                    // Reject the request if the offline_access scope was request and if
                    // the application is not allowed to use the refresh token grant type.
                    if (context.Request.HasScope(Scopes.OfflineAccess) &&
                       !await _applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.RefreshToken))
                    {
                        context.Logger.LogError("The authorization request was rejected because the application '{ClientId}' " +
                                                "was not allowed to request the 'offline_access' scope.", context.ClientId);

                        context.Reject(
                            error: Errors.InvalidRequest,
                            description: "The client application is not allowed to use the 'offline_access' scope.");

                        return;
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of rejecting authorization requests made by unauthorized applications.
            /// Note: this handler is not used when the degraded mode is enabled or when scope permissions are disabled.
            /// </summary>
            public class ValidateScopePermissions : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
            {
                private readonly IOpenIddictApplicationManager _applicationManager;

                public ValidateScopePermissions() => throw new InvalidOperationException(new StringBuilder()
                    .AppendLine("The core services must be registered when enabling the OpenIddict server feature.")
                    .Append("To register the OpenIddict core services, reference the 'OpenIddict.Core' package ")
                    .AppendLine("and call 'services.AddOpenIddict().AddCore()' from 'ConfigureServices'.")
                    .Append("Alternatively, you can disable the built-in database-based server features by enabling ")
                    .Append("the degraded mode with 'services.AddOpenIddict().AddServer().EnableDegradedMode()'.")
                    .ToString());

                public ValidateScopePermissions([NotNull] IOpenIddictApplicationManager applicationManager)
                    => _applicationManager = applicationManager;

                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateAuthorizationRequestContext>()
                        .AddFilter<RequireScopePermissionsEnabled>()
                        .AddFilter<RequireDegradedModeDisabled>()
                        .UseScopedHandler<ValidateScopePermissions>()
                        .SetOrder(ValidateGrantTypePermissions.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public async ValueTask HandleAsync([NotNull] ValidateAuthorizationRequestContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    var application = await _applicationManager.FindByClientIdAsync(context.ClientId);
                    if (application == null)
                    {
                        throw new InvalidOperationException("The client application details cannot be found in the database.");
                    }

                    foreach (var scope in context.Request.GetScopes())
                    {
                        // Avoid validating the "openid" and "offline_access" scopes as they represent protocol scopes.
                        if (string.Equals(scope, Scopes.OfflineAccess, StringComparison.Ordinal) ||
                            string.Equals(scope, Scopes.OpenId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        // Reject the request if the application is not allowed to use the iterated scope.
                        if (!await _applicationManager.HasPermissionAsync(application, Permissions.Prefixes.Scope + scope))
                        {
                            context.Logger.LogError("The authorization request was rejected because the application '{ClientId}' " +
                                                    "was not allowed to use the scope {Scope}.", context.ClientId, scope);

                            context.Reject(
                                error: Errors.InvalidRequest,
                                description: "This client application is not allowed to use the specified scope.");

                            return;
                        }
                    }
                }
            }

            /// <summary>
            /// Contains the logic responsible of inferring the redirect URL
            /// used to send the response back to the client application.
            /// </summary>
            public class AttachRedirectUri : IOpenIddictServerHandler<ApplyAuthorizationResponseContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyAuthorizationResponseContext>()
                        .UseSingletonHandler<AttachRedirectUri>()
                        .SetOrder(int.MinValue + 100_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ApplyAuthorizationResponseContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    if (context.Request == null)
                    {
                        return default;
                    }

                    // Note: at this stage, the validated redirect URI property may be null (e.g if an error
                    // is returned from the ExtractAuthorizationRequest/ValidateAuthorizationRequest events).
                    if (context.Transaction.Properties.TryGetValue(Properties.ValidatedRedirectUri, out var property))
                    {
                        context.RedirectUri = (string) property;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of inferring the response mode
            /// used to send the response back to the client application.
            /// </summary>
            public class InferResponseMode : IOpenIddictServerHandler<ApplyAuthorizationResponseContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyAuthorizationResponseContext>()
                        .UseSingletonHandler<InferResponseMode>()
                        .SetOrder(AttachRedirectUri.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ApplyAuthorizationResponseContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    if (context.Request == null)
                    {
                        return default;
                    }

                    context.ResponseMode = context.Request.ResponseMode;

                    // If the response_mode parameter was not specified, try to infer it.
                    if (string.IsNullOrEmpty(context.ResponseMode) && !string.IsNullOrEmpty(context.RedirectUri))
                    {
                        context.ResponseMode = context.Request.IsFormPostResponseMode() ? ResponseModes.FormPost :
                                               context.Request.IsFragmentResponseMode() ? ResponseModes.Fragment :
                                               context.Request.IsQueryResponseMode()    ? ResponseModes.Query    : null;
                    }

                    return default;
                }
            }

            /// <summary>
            /// Contains the logic responsible of attaching the state to the response.
            /// </summary>
            public class AttachResponseState : IOpenIddictServerHandler<ApplyAuthorizationResponseContext>
            {
                /// <summary>
                /// Gets the default descriptor definition assigned to this handler.
                /// </summary>
                public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                    = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyAuthorizationResponseContext>()
                        .UseSingletonHandler<AttachResponseState>()
                        .SetOrder(InferResponseMode.Descriptor.Order + 1_000)
                        .Build();

                /// <summary>
                /// Processes the event.
                /// </summary>
                /// <param name="context">The context associated with the event to process.</param>
                /// <returns>
                /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
                /// </returns>
                public ValueTask HandleAsync([NotNull] ApplyAuthorizationResponseContext context)
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException(nameof(context));
                    }

                    // Attach the request state to the authorization response.
                    if (string.IsNullOrEmpty(context.Response.State))
                    {
                        context.Response.State = context.Request?.State;
                    }

                    return default;
                }
            }
        }
    }
}
