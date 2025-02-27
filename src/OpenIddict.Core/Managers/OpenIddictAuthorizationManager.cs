﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace OpenIddict.Core
{
    /// <summary>
    /// Provides methods allowing to manage the authorizations stored in the store.
    /// </summary>
    /// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
    public class OpenIddictAuthorizationManager<TAuthorization> : IOpenIddictAuthorizationManager where TAuthorization : class
    {
        public OpenIddictAuthorizationManager(
            [NotNull] IOpenIddictAuthorizationCache<TAuthorization> cache,
            [NotNull] IOpenIddictAuthorizationStoreResolver resolver,
            [NotNull] ILogger<OpenIddictAuthorizationManager<TAuthorization>> logger,
            [NotNull] IOptionsMonitor<OpenIddictCoreOptions> options)
        {
            Cache = cache;
            Store = resolver.Get<TAuthorization>();
            Logger = logger;
            Options = options;
        }

        /// <summary>
        /// Gets the cache associated with the current manager.
        /// </summary>
        protected IOpenIddictAuthorizationCache<TAuthorization> Cache { get; }

        /// <summary>
        /// Gets the logger associated with the current manager.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the options associated with the current manager.
        /// </summary>
        protected IOptionsMonitor<OpenIddictCoreOptions> Options { get; }

        /// <summary>
        /// Gets the store associated with the current manager.
        /// </summary>
        protected IOpenIddictAuthorizationStore<TAuthorization> Store { get; }

        /// <summary>
        /// Determines the number of authorizations that exist in the database.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the number of authorizations in the database.
        /// </returns>
        public virtual ValueTask<long> CountAsync(CancellationToken cancellationToken = default)
            => Store.CountAsync(cancellationToken);

        /// <summary>
        /// Determines the number of authorizations that match the specified query.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="query">The query to execute.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the number of authorizations that match the specified query.
        /// </returns>
        public virtual ValueTask<long> CountAsync<TResult>(
            [NotNull] Func<IQueryable<TAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return Store.CountAsync(query, cancellationToken);
        }

        /// <summary>
        /// Creates a new authorization.
        /// </summary>
        /// <param name="authorization">The application to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual async ValueTask CreateAsync([NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            // If no status was explicitly specified, assume that the authorization is valid.
            if (string.IsNullOrEmpty(await Store.GetStatusAsync(authorization, cancellationToken)))
            {
                await Store.SetStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, cancellationToken);
            }

            var results = await ValidateAsync(authorization, cancellationToken).ToListAsync(cancellationToken);
            if (results.Any(result => result != ValidationResult.Success))
            {
                var builder = new StringBuilder();
                builder.AppendLine("One or more validation error(s) occurred while trying to create a new authorization:");
                builder.AppendLine();

                foreach (var result in results)
                {
                    builder.AppendLine(result.ErrorMessage);
                }

                throw new OpenIddictExceptions.ValidationException(builder.ToString(), results.ToImmutableArray());
            }

            await Store.CreateAsync(authorization, cancellationToken);

            if (!Options.CurrentValue.DisableEntityCaching)
            {
                await Cache.AddAsync(authorization, cancellationToken);
            }
        }

        /// <summary>
        /// Creates a new authorization based on the specified descriptor.
        /// </summary>
        /// <param name="descriptor">The authorization descriptor.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation, whose result returns the authorization.
        /// </returns>
        public virtual async ValueTask<TAuthorization> CreateAsync(
            [NotNull] OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var authorization = await Store.InstantiateAsync(cancellationToken);
            if (authorization == null)
            {
                throw new InvalidOperationException("An error occurred while trying to create a new authorization.");
            }

            await PopulateAsync(authorization, descriptor, cancellationToken);
            await CreateAsync(authorization, cancellationToken);

            return authorization;
        }

        /// <summary>
        /// Creates a new permanent authorization based on the specified parameters.
        /// </summary>
        /// <param name="claims">The claims associated with the authorization.</param>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="type">The authorization type.</param>
        /// <param name="scopes">The minimal scopes associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation, whose result returns the authorization.
        /// </returns>
        public virtual ValueTask<TAuthorization> CreateAsync(
            [NotNull] ImmutableDictionary<string, object> claims, [NotNull] string subject,
            [NotNull] string client, [NotNull] string type, ImmutableArray<string> scopes, CancellationToken cancellationToken = default)
        {
            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client identifier cannot be null or empty.", nameof(client));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("The type cannot be null or empty.", nameof(type));
            }

            var descriptor = new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = client,
                Status = OpenIddictConstants.Statuses.Valid,
                Subject = subject,
                Type = type
            };

            descriptor.Scopes.UnionWith(scopes);

            foreach (var claim in claims)
            {
                descriptor.Claims.Add(claim);
            }

            return CreateAsync(descriptor, cancellationToken);
        }

        /// <summary>
        /// Removes an existing authorization.
        /// </summary>
        /// <param name="authorization">The authorization to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual async ValueTask DeleteAsync([NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (!Options.CurrentValue.DisableEntityCaching)
            {
                await Cache.RemoveAsync(authorization, cancellationToken);
            }

            await Store.DeleteAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Retrieves the authorizations corresponding to the specified
        /// subject and associated with the application identifier.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the subject/client.</returns>
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
            [NotNull] string subject, [NotNull] string client, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client identifier cannot be null or empty.", nameof(client));
            }

            var authorizations = Options.CurrentValue.DisableEntityCaching ?
                Store.FindAsync(subject, client, cancellationToken) :
                Cache.FindAsync(subject, client, cancellationToken);

            // SQL engines like Microsoft SQL Server or MySQL are known to use case-insensitive lookups by default.
            // To ensure a case-sensitive comparison is enforced independently of the database/table/query collation
            // used by the store, a second pass using string.Equals(StringComparison.Ordinal) is manually made here.

            if (Options.CurrentValue.DisableAdditionalFiltering)
            {
                return authorizations;
            }

            return authorizations.WhereAwait(async authorization => string.Equals(
                await Store.GetSubjectAsync(authorization, cancellationToken), subject, StringComparison.Ordinal));
        }

        /// <summary>
        /// Retrieves the authorizations matching the specified parameters.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="status">The authorization status.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the criteria.</returns>
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
            [NotNull] string subject, [NotNull] string client,
            [NotNull] string status, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client identifier cannot be null or empty.", nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException("The status cannot be null or empty.", nameof(status));
            }

            var authorizations = Options.CurrentValue.DisableEntityCaching ?
                Store.FindAsync(subject, client, status, cancellationToken) :
                Cache.FindAsync(subject, client, status, cancellationToken);

            if (Options.CurrentValue.DisableAdditionalFiltering)
            {
                return authorizations;
            }

            // SQL engines like Microsoft SQL Server or MySQL are known to use case-insensitive lookups by default.
            // To ensure a case-sensitive comparison is enforced independently of the database/table/query collation
            // used by the store, a second pass using string.Equals(StringComparison.Ordinal) is manually made here.

            return authorizations.WhereAwait(async authorization => string.Equals(
                await Store.GetSubjectAsync(authorization, cancellationToken), subject, StringComparison.Ordinal));
        }

        /// <summary>
        /// Retrieves the authorizations matching the specified parameters.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="status">The authorization status.</param>
        /// <param name="type">The authorization type.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the criteria.</returns>
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
            [NotNull] string subject, [NotNull] string client,
            [NotNull] string status, [NotNull] string type, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client identifier cannot be null or empty.", nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException("The status cannot be null or empty.", nameof(status));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("The type cannot be null or empty.", nameof(type));
            }

            var authorizations = Options.CurrentValue.DisableEntityCaching ?
                Store.FindAsync(subject, client, status, type, cancellationToken) :
                Cache.FindAsync(subject, client, status, type, cancellationToken);

            if (Options.CurrentValue.DisableAdditionalFiltering)
            {
                return authorizations;
            }

            return authorizations.WhereAwait(async authorization => string.Equals(
                await Store.GetSubjectAsync(authorization, cancellationToken), subject, StringComparison.Ordinal));
        }

        /// <summary>
        /// Retrieves the authorizations matching the specified parameters.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="status">The authorization status.</param>
        /// <param name="type">The authorization type.</param>
        /// <param name="scopes">The minimal scopes associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the criteria.</returns>
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
            [NotNull] string subject, [NotNull] string client,
            [NotNull] string status, [NotNull] string type,
            ImmutableArray<string> scopes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("The client identifier cannot be null or empty.", nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException("The status cannot be null or empty.", nameof(status));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("The type cannot be null or empty.", nameof(type));
            }

            var authorizations = Options.CurrentValue.DisableEntityCaching ?
                Store.FindAsync(subject, client, status, type, scopes, cancellationToken) :
                Cache.FindAsync(subject, client, status, type, scopes, cancellationToken);

            if (Options.CurrentValue.DisableAdditionalFiltering)
            {
                return authorizations;
            }

            // SQL engines like Microsoft SQL Server or MySQL are known to use case-insensitive lookups by default.
            // To ensure a case-sensitive comparison is enforced independently of the database/table/query collation
            // used by the store, a second pass using string.Equals(StringComparison.Ordinal) is manually made here.

            return authorizations.WhereAwait(async authorization => string.Equals(
                await Store.GetSubjectAsync(authorization, cancellationToken), subject, StringComparison.Ordinal) &&
                await HasScopesAsync(authorization, scopes, cancellationToken));
        }

        /// <summary>
        /// Retrieves the list of authorizations corresponding to the specified application identifier.
        /// </summary>
        /// <param name="identifier">The application identifier associated with the authorizations.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the specified application.</returns>
        public virtual IAsyncEnumerable<TAuthorization> FindByApplicationIdAsync(
            [NotNull] string identifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
            }

            var authorizations = Options.CurrentValue.DisableEntityCaching ?
                Store.FindByApplicationIdAsync(identifier, cancellationToken) :
                Cache.FindByApplicationIdAsync(identifier, cancellationToken);

            if (Options.CurrentValue.DisableAdditionalFiltering)
            {
                return authorizations;
            }

            // SQL engines like Microsoft SQL Server or MySQL are known to use case-insensitive lookups by default.
            // To ensure a case-sensitive comparison is enforced independently of the database/table/query collation
            // used by the store, a second pass using string.Equals(StringComparison.Ordinal) is manually made here.

            return authorizations.WhereAwait(async authorization => string.Equals(
                await Store.GetApplicationIdAsync(authorization, cancellationToken), identifier, StringComparison.Ordinal));
        }

        /// <summary>
        /// Retrieves an authorization using its unique identifier.
        /// </summary>
        /// <param name="identifier">The unique identifier associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the authorization corresponding to the identifier.
        /// </returns>
        public virtual async ValueTask<TAuthorization> FindByIdAsync([NotNull] string identifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("The identifier cannot be null or empty.", nameof(identifier));
            }

            var authorization = Options.CurrentValue.DisableEntityCaching ?
                await Store.FindByIdAsync(identifier, cancellationToken) :
                await Cache.FindByIdAsync(identifier, cancellationToken);

            if (authorization == null)
            {
                return null;
            }

            // SQL engines like Microsoft SQL Server or MySQL are known to use case-insensitive lookups by default.
            // To ensure a case-sensitive comparison is enforced independently of the database/table/query collation
            // used by the store, a second pass using string.Equals(StringComparison.Ordinal) is manually made here.
            if (!Options.CurrentValue.DisableAdditionalFiltering &&
                !string.Equals(await Store.GetIdAsync(authorization, cancellationToken), identifier, StringComparison.Ordinal))
            {
                return null;
            }

            return authorization;
        }

        /// <summary>
        /// Retrieves all the authorizations corresponding to the specified subject.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the specified subject.</returns>
        public virtual IAsyncEnumerable<TAuthorization> FindBySubjectAsync(
            [NotNull] string subject, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("The subject cannot be null or empty.", nameof(subject));
            }

            var authorizations = Options.CurrentValue.DisableEntityCaching ?
                Store.FindBySubjectAsync(subject, cancellationToken) :
                Cache.FindBySubjectAsync(subject, cancellationToken);

            if (Options.CurrentValue.DisableAdditionalFiltering)
            {
                return authorizations;
            }

            // SQL engines like Microsoft SQL Server or MySQL are known to use case-insensitive lookups by default.
            // To ensure a case-sensitive comparison is enforced independently of the database/table/query collation
            // used by the store, a second pass using string.Equals(StringComparison.Ordinal) is manually made here.

            return authorizations.WhereAwait(async authorization => string.Equals(
                await Store.GetSubjectAsync(authorization, cancellationToken), subject, StringComparison.Ordinal));
        }

        /// <summary>
        /// Retrieves the optional application identifier associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the application identifier associated with the authorization.
        /// </returns>
        public virtual ValueTask<string> GetApplicationIdAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return Store.GetApplicationIdAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Executes the specified query and returns the first element.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="query">The query to execute.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the first element returned when executing the query.
        /// </returns>
        public virtual ValueTask<TResult> GetAsync<TResult>(
            [NotNull] Func<IQueryable<TAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return GetAsync((authorizations, state) => state(authorizations), query, cancellationToken);
        }

        /// <summary>
        /// Executes the specified query and returns the first element.
        /// </summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="query">The query to execute.</param>
        /// <param name="state">The optional state.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the first element returned when executing the query.
        /// </returns>
        public virtual ValueTask<TResult> GetAsync<TState, TResult>(
            [NotNull] Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
            [CanBeNull] TState state, CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return Store.GetAsync(query, state, cancellationToken);
        }

        /// <summary>
        /// Retrieves the unique identifier associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the unique identifier associated with the authorization.
        /// </returns>
        public virtual ValueTask<string> GetIdAsync([NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return Store.GetIdAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Retrieves the scopes associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the scopes associated with the specified authorization.
        /// </returns>
        public virtual ValueTask<ImmutableArray<string>> GetScopesAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return Store.GetScopesAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Retrieves the status associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the status associated with the specified authorization.
        /// </returns>
        public virtual ValueTask<string> GetStatusAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return Store.GetStatusAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Retrieves the subject associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the subject associated with the specified authorization.
        /// </returns>
        public virtual ValueTask<string> GetSubjectAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return Store.GetSubjectAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Retrieves the type associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the type associated with the specified authorization.
        /// </returns>
        public virtual ValueTask<string> GetTypeAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return Store.GetTypeAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Determines whether the specified scopes are included in the authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="scopes">The scopes.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the scopes are included in the authorization, <c>false</c> otherwise.</returns>
        public virtual async ValueTask<bool> HasScopesAsync([NotNull] TAuthorization authorization,
            ImmutableArray<string> scopes, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return (await Store.GetScopesAsync(authorization, cancellationToken))
                .ToImmutableHashSet(StringComparer.Ordinal)
                .IsSupersetOf(scopes);
        }

        /// <summary>
        /// Determines whether a given authorization is ad hoc.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization is ad hoc, <c>false</c> otherwise.</returns>
        public async ValueTask<bool> IsAdHocAsync([NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var type = await GetTypeAsync(authorization, cancellationToken);
            if (string.IsNullOrEmpty(type))
            {
                return false;
            }

            return string.Equals(type, OpenIddictConstants.AuthorizationTypes.AdHoc, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a given authorization is permanent.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization is permanent, <c>false</c> otherwise.</returns>
        public async ValueTask<bool> IsPermanentAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var type = await GetTypeAsync(authorization, cancellationToken);
            if (string.IsNullOrEmpty(type))
            {
                return false;
            }

            return string.Equals(type, OpenIddictConstants.AuthorizationTypes.Permanent, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a given authorization has been revoked.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization has been revoked, <c>false</c> otherwise.</returns>
        public virtual async ValueTask<bool> IsRevokedAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var status = await Store.GetStatusAsync(authorization, cancellationToken);
            if (string.IsNullOrEmpty(status))
            {
                return false;
            }

            return string.Equals(status, OpenIddictConstants.Statuses.Revoked, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a given authorization is valid.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization is valid, <c>false</c> otherwise.</returns>
        public virtual async ValueTask<bool> IsValidAsync(
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var status = await Store.GetStatusAsync(authorization, cancellationToken);
            if (string.IsNullOrEmpty(status))
            {
                return false;
            }

            return string.Equals(status, OpenIddictConstants.Statuses.Valid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Executes the specified query and returns all the corresponding elements.
        /// </summary>
        /// <param name="count">The number of results to return.</param>
        /// <param name="offset">The number of results to skip.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>All the elements returned when executing the specified query.</returns>
        public virtual IAsyncEnumerable<TAuthorization> ListAsync(
            [CanBeNull] int? count = null, [CanBeNull] int? offset = null, CancellationToken cancellationToken = default)
            => Store.ListAsync(count, offset, cancellationToken);

        /// <summary>
        /// Executes the specified query and returns all the corresponding elements.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="query">The query to execute.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>All the elements returned when executing the specified query.</returns>
        public virtual IAsyncEnumerable<TResult> ListAsync<TResult>(
            [NotNull] Func<IQueryable<TAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return ListAsync((authorizations, state) => state(authorizations), query, cancellationToken);
        }

        /// <summary>
        /// Executes the specified query and returns all the corresponding elements.
        /// </summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="query">The query to execute.</param>
        /// <param name="state">The optional state.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>All the elements returned when executing the specified query.</returns>
        public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
            [NotNull] Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
            [CanBeNull] TState state, CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return Store.ListAsync(query, state, cancellationToken);
        }

        /// <summary>
        /// Populates the authorization using the specified descriptor.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual async ValueTask PopulateAsync([NotNull] TAuthorization authorization,
            [NotNull] OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            await Store.SetApplicationIdAsync(authorization, descriptor.ApplicationId, cancellationToken);
            await Store.SetScopesAsync(authorization, ImmutableArray.CreateRange(descriptor.Scopes), cancellationToken);
            await Store.SetStatusAsync(authorization, descriptor.Status, cancellationToken);
            await Store.SetSubjectAsync(authorization, descriptor.Subject, cancellationToken);
            await Store.SetTypeAsync(authorization, descriptor.Type, cancellationToken);
        }

        /// <summary>
        /// Populates the specified descriptor using the properties exposed by the authorization.
        /// </summary>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual async ValueTask PopulateAsync(
            [NotNull] OpenIddictAuthorizationDescriptor descriptor,
            [NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            descriptor.ApplicationId = await Store.GetApplicationIdAsync(authorization, cancellationToken);
            descriptor.Scopes.Clear();
            descriptor.Scopes.UnionWith(await Store.GetScopesAsync(authorization, cancellationToken));
            descriptor.Status = await Store.GetStatusAsync(authorization, cancellationToken);
            descriptor.Subject = await Store.GetSubjectAsync(authorization, cancellationToken);
            descriptor.Type = await Store.GetTypeAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Removes the authorizations that are marked as invalid and the ad-hoc ones that have no valid/nonexpired token attached.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual ValueTask PruneAsync(CancellationToken cancellationToken = default)
            => Store.PruneAsync(cancellationToken);

        /// <summary>
        /// Revokes an authorization.
        /// </summary>
        /// <param name="authorization">The authorization to revoke.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
        public virtual async ValueTask RevokeAsync([NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var status = await Store.GetStatusAsync(authorization, cancellationToken);
            if (!string.Equals(status, OpenIddictConstants.Statuses.Revoked, StringComparison.OrdinalIgnoreCase))
            {
                await Store.SetStatusAsync(authorization, OpenIddictConstants.Statuses.Revoked, cancellationToken);
                await UpdateAsync(authorization, cancellationToken);
            }
        }

        /// <summary>
        /// Sets the application identifier associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="identifier">The unique identifier associated with the client application.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual async ValueTask SetApplicationIdAsync(
            [NotNull] TAuthorization authorization, [CanBeNull] string identifier, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            await Store.SetApplicationIdAsync(authorization, identifier, cancellationToken);
            await UpdateAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Updates an existing authorization.
        /// </summary>
        /// <param name="authorization">The authorization to update.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual async ValueTask UpdateAsync([NotNull] TAuthorization authorization, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var results = await ValidateAsync(authorization, cancellationToken).ToListAsync(cancellationToken);
            if (results.Any(result => result != ValidationResult.Success))
            {
                var builder = new StringBuilder();
                builder.AppendLine("One or more validation error(s) occurred while trying to update an existing authorization:");
                builder.AppendLine();

                foreach (var result in results)
                {
                    builder.AppendLine(result.ErrorMessage);
                }

                throw new OpenIddictExceptions.ValidationException(builder.ToString(), results.ToImmutableArray());
            }

            await Store.UpdateAsync(authorization, cancellationToken);

            if (!Options.CurrentValue.DisableEntityCaching)
            {
                await Cache.RemoveAsync(authorization, cancellationToken);
                await Cache.AddAsync(authorization, cancellationToken);
            }
        }

        /// <summary>
        /// Updates an existing authorization.
        /// </summary>
        /// <param name="authorization">The authorization to update.</param>
        /// <param name="descriptor">The descriptor used to update the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        public virtual async ValueTask UpdateAsync([NotNull] TAuthorization authorization,
            [NotNull] OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            await PopulateAsync(authorization, descriptor, cancellationToken);
            await UpdateAsync(authorization, cancellationToken);
        }

        /// <summary>
        /// Validates the authorization to ensure it's in a consistent state.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The validation error encountered when validating the authorization.</returns>
        public virtual async IAsyncEnumerable<ValidationResult> ValidateAsync(
            [NotNull] TAuthorization authorization, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var builder = ImmutableArray.CreateBuilder<ValidationResult>();

            var type = await Store.GetTypeAsync(authorization, cancellationToken);
            if (string.IsNullOrEmpty(type))
            {
                yield return new ValidationResult("The authorization type cannot be null or empty.");
            }

            else if (!string.Equals(type, OpenIddictConstants.AuthorizationTypes.AdHoc, StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(type, OpenIddictConstants.AuthorizationTypes.Permanent, StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult("The specified authorization type is not supported by the default token manager.");
            }

            if (string.IsNullOrEmpty(await Store.GetStatusAsync(authorization, cancellationToken)))
            {
                yield return new ValidationResult("The status cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(await Store.GetSubjectAsync(authorization, cancellationToken)))
            {
                yield return new ValidationResult("The subject cannot be null or empty.");
            }

            // Ensure that the scopes are not null or empty and do not contain spaces.
            foreach (var scope in await Store.GetScopesAsync(authorization, cancellationToken))
            {
                if (string.IsNullOrEmpty(scope))
                {
                    yield return new ValidationResult("Scopes cannot be null or empty.");

                    break;
                }

                if (scope.Contains(OpenIddictConstants.Separators.Space[0]))
                {
                    yield return new ValidationResult("Scopes cannot contain spaces.");

                    break;
                }
            }
        }

        ValueTask<long> IOpenIddictAuthorizationManager.CountAsync(CancellationToken cancellationToken)
            => CountAsync(cancellationToken);

        ValueTask<long> IOpenIddictAuthorizationManager.CountAsync<TResult>(Func<IQueryable<object>, IQueryable<TResult>> query, CancellationToken cancellationToken)
            => CountAsync(query, cancellationToken);

        async ValueTask<object> IOpenIddictAuthorizationManager.CreateAsync(ImmutableDictionary<string, object> claims, string subject, string client, string type, ImmutableArray<string> scopes, CancellationToken cancellationToken)
            => await CreateAsync(claims, subject, client, type, scopes, cancellationToken);

        async ValueTask<object> IOpenIddictAuthorizationManager.CreateAsync(OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken)
            => await CreateAsync(descriptor, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.CreateAsync(object authorization, CancellationToken cancellationToken)
            => CreateAsync((TAuthorization) authorization, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.DeleteAsync(object authorization, CancellationToken cancellationToken)
            => DeleteAsync((TAuthorization) authorization, cancellationToken);

        IAsyncEnumerable<object> IOpenIddictAuthorizationManager.FindAsync(string subject, string client, CancellationToken cancellationToken)
            => FindAsync(subject, client, cancellationToken).OfType<object>();

        IAsyncEnumerable<object> IOpenIddictAuthorizationManager.FindAsync(string subject, string client, string status, CancellationToken cancellationToken)
            => FindAsync(subject, client, status, cancellationToken).OfType<object>();

        IAsyncEnumerable<object> IOpenIddictAuthorizationManager.FindAsync(string subject, string client, string status, string type, CancellationToken cancellationToken)
            => FindAsync(subject, client, status, type, cancellationToken).OfType<object>();

        IAsyncEnumerable<object> IOpenIddictAuthorizationManager.FindAsync(string subject, string client, string status, string type, ImmutableArray<string> scopes, CancellationToken cancellationToken)
            => FindAsync(subject, client, status, type, scopes, cancellationToken).OfType<object>();

        IAsyncEnumerable<object> IOpenIddictAuthorizationManager.FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
            => FindByApplicationIdAsync(identifier, cancellationToken).OfType<object>();

        async ValueTask<object> IOpenIddictAuthorizationManager.FindByIdAsync(string identifier, CancellationToken cancellationToken)
            => await FindByIdAsync(identifier, cancellationToken);

        IAsyncEnumerable<object> IOpenIddictAuthorizationManager.FindBySubjectAsync(string subject, CancellationToken cancellationToken)
            => FindBySubjectAsync(subject, cancellationToken).OfType<object>();

        ValueTask<string> IOpenIddictAuthorizationManager.GetApplicationIdAsync(object authorization, CancellationToken cancellationToken)
            => GetApplicationIdAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<TResult> IOpenIddictAuthorizationManager.GetAsync<TResult>(Func<IQueryable<object>, IQueryable<TResult>> query, CancellationToken cancellationToken)
            => GetAsync(query, cancellationToken);

        ValueTask<TResult> IOpenIddictAuthorizationManager.GetAsync<TState, TResult>(Func<IQueryable<object>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
            => GetAsync(query, state, cancellationToken);

        ValueTask<string> IOpenIddictAuthorizationManager.GetIdAsync(object authorization, CancellationToken cancellationToken)
            => GetIdAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<ImmutableArray<string>> IOpenIddictAuthorizationManager.GetScopesAsync(object authorization, CancellationToken cancellationToken)
            => GetScopesAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<string> IOpenIddictAuthorizationManager.GetStatusAsync(object authorization, CancellationToken cancellationToken)
            => GetStatusAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<string> IOpenIddictAuthorizationManager.GetSubjectAsync(object authorization, CancellationToken cancellationToken)
            => GetSubjectAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<string> IOpenIddictAuthorizationManager.GetTypeAsync(object authorization, CancellationToken cancellationToken)
            => GetTypeAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<bool> IOpenIddictAuthorizationManager.HasScopesAsync(object authorization, ImmutableArray<string> scopes, CancellationToken cancellationToken)
            => HasScopesAsync((TAuthorization) authorization, scopes, cancellationToken);

        ValueTask<bool> IOpenIddictAuthorizationManager.IsAdHocAsync(object authorization, CancellationToken cancellationToken)
            => IsAdHocAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<bool> IOpenIddictAuthorizationManager.IsPermanentAsync(object authorization, CancellationToken cancellationToken)
            => IsPermanentAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<bool> IOpenIddictAuthorizationManager.IsRevokedAsync(object authorization, CancellationToken cancellationToken)
            => IsRevokedAsync((TAuthorization) authorization, cancellationToken);

        ValueTask<bool> IOpenIddictAuthorizationManager.IsValidAsync(object authorization, CancellationToken cancellationToken)
            => IsValidAsync((TAuthorization) authorization, cancellationToken);

        IAsyncEnumerable<object> IOpenIddictAuthorizationManager.ListAsync(int? count, int? offset, CancellationToken cancellationToken)
            => ListAsync(count, offset, cancellationToken).OfType<object>();

        IAsyncEnumerable<TResult> IOpenIddictAuthorizationManager.ListAsync<TResult>(Func<IQueryable<object>, IQueryable<TResult>> query, CancellationToken cancellationToken)
            => ListAsync(query, cancellationToken);

        IAsyncEnumerable<TResult> IOpenIddictAuthorizationManager.ListAsync<TState, TResult>(Func<IQueryable<object>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
            => ListAsync(query, state, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.PopulateAsync(OpenIddictAuthorizationDescriptor descriptor, object authorization, CancellationToken cancellationToken)
            => PopulateAsync(descriptor, (TAuthorization) authorization, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.PopulateAsync(object authorization, OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken)
            => PopulateAsync((TAuthorization) authorization, descriptor, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.PruneAsync(CancellationToken cancellationToken)
            => PruneAsync(cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.RevokeAsync(object authorization, CancellationToken cancellationToken)
            => RevokeAsync((TAuthorization) authorization, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.SetApplicationIdAsync(object authorization, string identifier, CancellationToken cancellationToken)
            => SetApplicationIdAsync((TAuthorization) authorization, identifier, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.UpdateAsync(object authorization, CancellationToken cancellationToken)
            => UpdateAsync((TAuthorization) authorization, cancellationToken);

        ValueTask IOpenIddictAuthorizationManager.UpdateAsync(object authorization, OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken)
            => UpdateAsync((TAuthorization) authorization, descriptor, cancellationToken);

        IAsyncEnumerable<ValidationResult> IOpenIddictAuthorizationManager.ValidateAsync(object authorization, CancellationToken cancellationToken)
            => ValidateAsync((TAuthorization) authorization, cancellationToken);
    }
}