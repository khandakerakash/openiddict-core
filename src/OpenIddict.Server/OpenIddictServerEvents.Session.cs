﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using JetBrains.Annotations;

namespace OpenIddict.Server
{
    public static partial class OpenIddictServerEvents
    {
        /// <summary>
        /// Represents an event called for each request to the logout endpoint to give the user code
        /// a chance to manually extract the logout request from the ambient HTTP context.
        /// </summary>
        public class ExtractLogoutRequestContext : BaseValidatingContext
        {
            /// <summary>
            /// Creates a new instance of the <see cref="ExtractLogoutRequestContext"/> class.
            /// </summary>
            public ExtractLogoutRequestContext([NotNull] OpenIddictServerTransaction transaction)
                : base(transaction)
            {
            }
        }

        /// <summary>
        /// Represents an event called for each request to the logout endpoint
        /// to determine if the request is valid and should continue to be processed.
        /// </summary>
        public class ValidateLogoutRequestContext : BaseValidatingContext
        {
            /// <summary>
            /// Creates a new instance of the <see cref="ValidateLogoutRequestContext"/> class.
            /// </summary>
            public ValidateLogoutRequestContext([NotNull] OpenIddictServerTransaction transaction)
                : base(transaction)
                // Infer the post_logout_redirect_uri from the value specified by the client application.
                => PostLogoutRedirectUri = Request?.PostLogoutRedirectUri;

            /// <summary>
            /// Gets the post_logout_redirect_uri specified by the client application.
            /// </summary>
            public string PostLogoutRedirectUri { get; private set; }

            /// <summary>
            /// Populates the <see cref="PostLogoutRedirectUri"/> property with the specified redirect_uri.
            /// </summary>
            /// <param name="address">The post_logout_redirect_uri to use when redirecting the user agent.</param>
            public void SetPostLogoutRedirectUri(string address)
            {
                if (string.IsNullOrEmpty(address))
                {
                    throw new ArgumentException("The post_logout_redirect_uri cannot be null or empty.", nameof(address));
                }

                // Don't allow validation to alter the post_logout_redirect_uri parameter extracted
                // from the request if the address was explicitly provided by the client application.
                if (!string.IsNullOrEmpty(Request.PostLogoutRedirectUri) &&
                    !string.Equals(Request.PostLogoutRedirectUri, address, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The end session request cannot be validated because a different " +
                        "post_logout_redirect_uri was specified by the client application.");
                }

                PostLogoutRedirectUri = address;
            }
        }

        /// <summary>
        /// Represents an event called for each validated logout request
        /// to allow the user code to decide how the request should be handled.
        /// </summary>
        public class HandleLogoutRequestContext : BaseValidatingContext
        {
            /// <summary>
            /// Creates a new instance of the <see cref="HandleLogoutRequestContext"/> class.
            /// </summary>
            public HandleLogoutRequestContext([NotNull] OpenIddictServerTransaction transaction)
                : base(transaction)
            {
            }

            /// <summary>
            /// Gets a boolean indicating whether the logout request should be processed.
            /// </summary>
            public bool IsLogoutAllowed { get; private set; }

            /// <summary>
            /// Allow the logout request to be processed.
            /// </summary>
            public void ProcessLogout() => IsLogoutAllowed = true;
        }

        /// <summary>
        /// Represents an event called before the logout response is returned to the caller.
        /// </summary>
        public class ApplyLogoutResponseContext : BaseRequestContext
        {
            /// <summary>
            /// Creates a new instance of the <see cref="ApplyLogoutResponseContext"/> class.
            /// </summary>
            public ApplyLogoutResponseContext([NotNull] OpenIddictServerTransaction transaction)
                : base(transaction)
            {
            }

            /// <summary>
            /// Gets the error code returned to the client application.
            /// When the response indicates a successful response,
            /// this property returns <c>null</c>.
            /// </summary>
            public string Error => Response.Error;

            /// <summary>
            /// Gets or sets the callback URL the user agent will be redirected to, if applicable.
            /// Note: manually changing the value of this property is generally not recommended
            /// and extreme caution must be taken to ensure the user agent is not redirected to
            /// an untrusted address, which would result in an "open redirection" vulnerability.
            /// </summary>
            public string PostLogoutRedirectUri { get; set; }
        }
    }
}
