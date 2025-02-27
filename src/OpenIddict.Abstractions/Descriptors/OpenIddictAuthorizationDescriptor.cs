﻿using System;
using System.Collections.Generic;

namespace OpenIddict.Abstractions
{
    /// <summary>
    /// Represents an OpenIddict authorization descriptor.
    /// </summary>
    public class OpenIddictAuthorizationDescriptor
    {
        /// <summary>
        /// Gets or sets the application identifier associated with the authorization.
        /// </summary>
        public string ApplicationId { get; set; }

        /// <summary>
        /// Gets the claims associated with the authorization.
        /// Note: this property is not stored by the default authorization stores.
        /// </summary>
        public IDictionary<string, object> Claims { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the scopes associated with the authorization.
        /// </summary>
        public ISet<string> Scopes { get; } =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the status associated with the authorization.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the subject associated with the authorization.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the type of the authorization.
        /// </summary>
        public virtual string Type { get; set; }
    }
}
