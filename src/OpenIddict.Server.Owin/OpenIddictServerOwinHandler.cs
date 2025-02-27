﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using Properties = OpenIddict.Server.Owin.OpenIddictServerOwinConstants.Properties;

namespace OpenIddict.Server.Owin
{
    /// <summary>
    /// Provides the entry point necessary to register the OpenIddict server in an OWIN pipeline.
    /// </summary>
    public class OpenIddictServerOwinHandler : AuthenticationHandler<OpenIddictServerOwinOptions>
    {
        private readonly ILogger _logger;
        private readonly IOpenIddictServerProvider _provider;

        /// <summary>
        /// Creates a new instance of the <see cref="OpenIddictServerOwinHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger used by this instance.</param>
        /// <param name="provider">The OpenIddict server OWIN provider used by this instance.</param>
        public OpenIddictServerOwinHandler(
            [NotNull] ILogger logger,
            [NotNull] IOpenIddictServerProvider provider)
        {
            _logger = logger;
            _provider = provider;
        }

        public override async Task<bool> InvokeAsync()
        {
            // Note: the transaction may be already attached when replaying an OWIN request
            // (e.g when using a status code pages middleware re-invoking the OWIN pipeline).
            var transaction = Context.Get<OpenIddictServerTransaction>(typeof(OpenIddictServerTransaction).FullName);
            if (transaction == null)
            {
                // Create a new transaction and attach the OWIN request to make it available to the OWIN handlers.
                transaction = await _provider.CreateTransactionAsync();
                transaction.Properties[typeof(IOwinRequest).FullName] = new WeakReference<IOwinRequest>(Request);

                // Attach the OpenIddict server transaction to the OWIN shared dictionary
                // so that it can retrieved while performing sign-in/sign-out operations.
                Context.Set(typeof(OpenIddictServerTransaction).FullName, transaction);
            }

            var context = new ProcessRequestContext(transaction);
            await _provider.DispatchAsync(context);

            if (context.IsRequestHandled)
            {
                return true;
            }

            else if (context.IsRequestSkipped)
            {
                return false;
            }

            else if (context.IsRejected)
            {
                var notification = new ProcessErrorResponseContext(transaction)
                {
                    Response = new OpenIddictResponse
                    {
                        Error = context.Error ?? Errors.InvalidRequest,
                        ErrorDescription = context.ErrorDescription,
                        ErrorUri = context.ErrorUri
                    }
                };

                await _provider.DispatchAsync(notification);

                if (notification.IsRequestHandled)
                {
                    return true;
                }

                else if (notification.IsRequestSkipped)
                {
                    return false;
                }

                throw new InvalidOperationException(new StringBuilder()
                    .Append("The OpenID Connect response was not correctly processed. This may indicate ")
                    .Append("that the event handler responsible of processing OpenID Connect responses ")
                    .Append("was not registered or was explicitly removed from the handlers list.")
                    .ToString());
            }

            return false;
        }

        protected override async Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            var transaction = Context.Get<OpenIddictServerTransaction>(typeof(OpenIddictServerTransaction).FullName);
            if (transaction?.Request == null)
            {
                throw new InvalidOperationException("An identity cannot be extracted from this request.");
            }

            switch (transaction.EndpointType)
            {
                case OpenIddictServerEndpointType.Authorization:
                case OpenIddictServerEndpointType.Logout:
                {
                    if (string.IsNullOrEmpty(transaction.Request.IdTokenHint))
                    {
                        return null;
                    }

                    var notification = new DeserializeIdentityTokenContext(transaction)
                    {
                        Token = transaction.Request.IdTokenHint
                    };

                    await _provider.DispatchAsync(notification);

                    if (!notification.IsHandled)
                    {
                        throw new InvalidOperationException(new StringBuilder()
                            .Append("The identity token was not correctly processed. This may indicate ")
                            .Append("that the event handler responsible of validating identity tokens ")
                            .Append("was not registered or was explicitly removed from the handlers list.")
                            .ToString());
                    }

                    if (notification.Principal == null)
                    {
                        _logger.LogWarning("The identity token extracted from the 'id_token_hint' " +
                                           "parameter was invalid or malformed and was ignored.");

                        return null;
                    }

                    // Tickets are returned even if they are considered invalid (e.g expired).

                    return new AuthenticationTicket((ClaimsIdentity) notification.Principal.Identity, new AuthenticationProperties());
                }

                case OpenIddictServerEndpointType.Token when transaction.Request.IsAuthorizationCodeGrantType():
                {
                    // Note: this method can be called from the ApplyTokenResponse event,
                    // which may be invoked for a missing authorization code/refresh token.
                    if (string.IsNullOrEmpty(transaction.Request.Code))
                    {
                        return null;
                    }

                    var notification = new DeserializeAuthorizationCodeContext(transaction)
                    {
                        Token = transaction.Request.Code
                    };

                    await _provider.DispatchAsync(notification);

                    if (!notification.IsHandled)
                    {
                        throw new InvalidOperationException(new StringBuilder()
                            .Append("The authorization code was not correctly processed. This may indicate ")
                            .Append("that the event handler responsible of validating authorization codes ")
                            .Append("was not registered or was explicitly removed from the handlers list.")
                            .ToString());
                    }

                    if (notification.Principal == null)
                    {
                        _logger.LogWarning("The authorization code extracted from the token request was invalid and was ignored.");

                        return null;
                    }

                    // Tickets are returned even if they are considered invalid (e.g expired).

                    return new AuthenticationTicket((ClaimsIdentity) notification.Principal.Identity, new AuthenticationProperties());
                }

                case OpenIddictServerEndpointType.Token when transaction.Request.IsRefreshTokenGrantType():
                {
                    if (string.IsNullOrEmpty(transaction.Request.RefreshToken))
                    {
                        return null;
                    }

                    var notification = new DeserializeRefreshTokenContext(transaction)
                    {
                        Token = transaction.Request.RefreshToken
                    };

                    await _provider.DispatchAsync(notification);

                    if (!notification.IsHandled)
                    {
                        throw new InvalidOperationException(new StringBuilder()
                            .Append("The refresh token was not correctly processed. This may indicate ")
                            .Append("that the event handler responsible of validating refresh tokens ")
                            .Append("was not registered or was explicitly removed from the handlers list.")
                            .ToString());
                    }

                    if (notification.Principal == null)
                    {
                        _logger.LogWarning("The refresh token extracted from the token request was invalid and was ignored.");

                        return null;
                    }

                    // Tickets are returned even if they are considered invalid (e.g expired).

                    return new AuthenticationTicket((ClaimsIdentity) notification.Principal.Identity, new AuthenticationProperties());
                }

                case OpenIddictServerEndpointType.Userinfo:
                {
                    if (string.IsNullOrEmpty(transaction.Request.AccessToken))
                    {
                        return null;
                    }

                    var notification = new DeserializeAccessTokenContext(transaction)
                    {
                        Token = transaction.Request.AccessToken
                    };

                    await _provider.DispatchAsync(notification);

                    if (!notification.IsHandled)
                    {
                        throw new InvalidOperationException(new StringBuilder()
                            .Append("The access token was not correctly processed. This may indicate ")
                            .Append("that the event handler responsible of validating access tokens ")
                            .Append("was not registered or was explicitly removed from the handlers list.")
                            .ToString());
                    }

                    if (notification.Principal == null)
                    {
                        _logger.LogWarning("The access token extracted from the userinfo request was invalid and was ignored.");

                        return null;
                    }

                    var date = notification.Principal.GetExpirationDate();
                    if (date.HasValue && date.Value < DateTimeOffset.UtcNow)
                    {
                        _logger.LogError("The access token extracted from the userinfo request was expired.");

                        return null;
                    }

                    return new AuthenticationTicket((ClaimsIdentity) notification.Principal.Identity, new AuthenticationProperties());
                }

                default: throw new InvalidOperationException("An identity cannot be extracted from this request.");
            }
        }

        protected override async Task TeardownCoreAsync()
        {
            // Note: OWIN authentication handlers cannot reliabily write to the response stream
            // from ApplyResponseGrantAsync or ApplyResponseChallengeAsync because these methods
            // are susceptible to be invoked from AuthenticationHandler.OnSendingHeaderCallback,
            // where calling Write or WriteAsync on the response stream may result in a deadlock
            // on hosts using streamed responses. To work around this limitation, this handler
            // doesn't implement ApplyResponseGrantAsync but TeardownCoreAsync, which is never called
            // by AuthenticationHandler.OnSendingHeaderCallback. In theory, this would prevent
            // OpenIddictServerOwinMiddleware from both applying the response grant and allowing
            // the next middleware in the pipeline to alter the response stream but in practice,
            // OpenIddictServerOwinMiddleware is assumed to be the only middleware allowed to write
            // to the response stream when a response grant (sign-in/out or challenge) was applied.

            var challenge = Helper.LookupChallenge(Options.AuthenticationType, Options.AuthenticationMode);
            if (challenge != null)
            {
                var transaction = Context.Get<OpenIddictServerTransaction>(typeof(OpenIddictServerTransaction).FullName);
                if (transaction == null)
                {
                    throw new InvalidOperationException("An OpenID Connect response cannot be returned from this endpoint.");
                }

                var context = new ProcessChallengeResponseContext(transaction)
                {
                    Response = new OpenIddictResponse
                    {
                        Error = GetProperty(challenge.Properties, Properties.Error),
                        ErrorDescription = GetProperty(challenge.Properties, Properties.ErrorDescription),
                        ErrorUri = GetProperty(challenge.Properties, Properties.ErrorUri)
                    }
                };

                await _provider.DispatchAsync(context);

                if (context.IsRequestHandled || context.IsRequestSkipped)
                {
                    return;
                }

                else if (context.IsRejected)
                {
                    var notification = new ProcessErrorResponseContext(transaction)
                    {
                        Response = new OpenIddictResponse
                        {
                            Error = context.Error ?? Errors.InvalidRequest,
                            ErrorDescription = context.ErrorDescription,
                            ErrorUri = context.ErrorUri
                        }
                    };

                    await _provider.DispatchAsync(notification);

                    if (notification.IsRequestHandled || context.IsRequestSkipped)
                    {
                        return;
                    }

                    throw new InvalidOperationException(new StringBuilder()
                        .Append("The OpenID Connect response was not correctly processed. This may indicate ")
                        .Append("that the event handler responsible of processing OpenID Connect responses ")
                        .Append("was not registered or was explicitly removed from the handlers list.")
                        .ToString());
                }

                static string GetProperty(AuthenticationProperties properties, string name)
                    => properties != null && properties.Dictionary.TryGetValue(name, out string value) ? value : null;
            }

            var signin = Helper.LookupSignIn(Options.AuthenticationType);
            if (signin != null)
            {
                var transaction = Context.Get<OpenIddictServerTransaction>(typeof(OpenIddictServerTransaction).FullName);
                if (transaction == null)
                {
                    throw new InvalidOperationException("An OpenID Connect response cannot be returned from this endpoint.");
                }

                var context = new ProcessSigninResponseContext(transaction)
                {
                    Principal = signin.Principal,
                    Response = new OpenIddictResponse()
                };

                await _provider.DispatchAsync(context);

                if (context.IsRequestHandled || context.IsRequestSkipped)
                {
                    return;
                }

                else if (context.IsRejected)
                {
                    var notification = new ProcessErrorResponseContext(transaction)
                    {
                        Response = new OpenIddictResponse
                        {
                            Error = context.Error ?? Errors.InvalidRequest,
                            ErrorDescription = context.ErrorDescription,
                            ErrorUri = context.ErrorUri
                        }
                    };

                    await _provider.DispatchAsync(notification);

                    if (notification.IsRequestHandled || context.IsRequestSkipped)
                    {
                        return;
                    }

                    throw new InvalidOperationException(new StringBuilder()
                        .Append("The OpenID Connect response was not correctly processed. This may indicate ")
                        .Append("that the event handler responsible of processing OpenID Connect responses ")
                        .Append("was not registered or was explicitly removed from the handlers list.")
                        .ToString());
                }
            }

            var signout = Helper.LookupSignOut(Options.AuthenticationType, Options.AuthenticationMode);
            if (signout != null)
            {
                var transaction = Context.Get<OpenIddictServerTransaction>(typeof(OpenIddictServerTransaction).FullName);
                if (transaction == null)
                {
                    throw new InvalidOperationException("An OpenID Connect response cannot be returned from this endpoint.");
                }

                var context = new ProcessSignoutResponseContext(transaction)
                {
                    Response = new OpenIddictResponse()
                };

                await _provider.DispatchAsync(context);

                if (context.IsRequestHandled || context.IsRequestSkipped)
                {
                    return;
                }

                else if (context.IsRejected)
                {
                    var notification = new ProcessErrorResponseContext(transaction)
                    {
                        Response = new OpenIddictResponse
                        {
                            Error = context.Error ?? Errors.InvalidRequest,
                            ErrorDescription = context.ErrorDescription,
                            ErrorUri = context.ErrorUri
                        }
                    };

                    await _provider.DispatchAsync(notification);

                    if (notification.IsRequestHandled || context.IsRequestSkipped)
                    {
                        return;
                    }

                    throw new InvalidOperationException(new StringBuilder()
                        .Append("The OpenID Connect response was not correctly processed. This may indicate ")
                        .Append("that the event handler responsible of processing OpenID Connect responses ")
                        .Append("was not registered or was explicitly removed from the handlers list.")
                        .ToString());
                }
            }
        }
    }
}
