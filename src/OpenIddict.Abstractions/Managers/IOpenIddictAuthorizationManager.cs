﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace OpenIddict.Abstractions
{
    /// <summary>
    /// Provides methods allowing to manage the authorizations stored in the store.
    /// Note: this interface is not meant to be implemented by custom managers,
    /// that should inherit from the generic OpenIddictAuthorizationManager class.
    /// It is primarily intended to be used by services that cannot easily depend
    /// on the generic authorization manager. The actual authorization entity type
    /// is automatically determined at runtime based on the OpenIddict core options.
    /// </summary>
    public interface IOpenIddictAuthorizationManager
    {
        /// <summary>
        /// Determines the number of authorizations that exist in the database.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the number of authorizations in the database.
        /// </returns>
        ValueTask<long> CountAsync(CancellationToken cancellationToken = default);

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
        ValueTask<long> CountAsync<TResult>(
            [NotNull] Func<IQueryable<object>, IQueryable<TResult>> query, CancellationToken cancellationToken = default);

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
        ValueTask<object> CreateAsync(
            [NotNull] ImmutableDictionary<string, object> claims, [NotNull] string subject, [NotNull] string client,
            [NotNull] string type, ImmutableArray<string> scopes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new authorization based on the specified descriptor.
        /// </summary>
        /// <param name="descriptor">The authorization descriptor.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation, whose result returns the authorization.
        /// </returns>
        ValueTask<object> CreateAsync([NotNull] OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new authorization.
        /// </summary>
        /// <param name="authorization">The application to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask CreateAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an existing authorization.
        /// </summary>
        /// <param name="authorization">The authorization to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask DeleteAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the authorizations corresponding to the specified
        /// subject and associated with the application identifier.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the subject/client.</returns>
        IAsyncEnumerable<object> FindAsync([NotNull] string subject, [NotNull] string client, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the authorizations matching the specified parameters.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="status">The authorization status.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the criteria.</returns>
        IAsyncEnumerable<object> FindAsync(
            [NotNull] string subject, [NotNull] string client,
            [NotNull] string status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the authorizations matching the specified parameters.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="client">The client associated with the authorization.</param>
        /// <param name="status">The authorization status.</param>
        /// <param name="type">The authorization type.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the criteria.</returns>
        IAsyncEnumerable<object> FindAsync(
            [NotNull] string subject, [NotNull] string client,
            [NotNull] string status, [NotNull] string type, CancellationToken cancellationToken = default);

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
        IAsyncEnumerable<object> FindAsync(
            [NotNull] string subject, [NotNull] string client, [NotNull] string status,
            [NotNull] string type, ImmutableArray<string> scopes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the list of authorizations corresponding to the specified application identifier.
        /// </summary>
        /// <param name="identifier">The application identifier associated with the authorizations.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the specified application.</returns>
        IAsyncEnumerable<object> FindByApplicationIdAsync([NotNull] string identifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an authorization using its unique identifier.
        /// </summary>
        /// <param name="identifier">The unique identifier associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the authorization corresponding to the identifier.
        /// </returns>
        ValueTask<object> FindByIdAsync([NotNull] string identifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all the authorizations corresponding to the specified subject.
        /// </summary>
        /// <param name="subject">The subject associated with the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The authorizations corresponding to the specified subject.</returns>
        IAsyncEnumerable<object> FindBySubjectAsync([NotNull] string subject, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the optional application identifier associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the application identifier associated with the authorization.
        /// </returns>
        ValueTask<string> GetApplicationIdAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

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
        ValueTask<TResult> GetAsync<TResult>(
            [NotNull] Func<IQueryable<object>, IQueryable<TResult>> query, CancellationToken cancellationToken = default);

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
        ValueTask<TResult> GetAsync<TState, TResult>(
            [NotNull] Func<IQueryable<object>, TState, IQueryable<TResult>> query,
            [CanBeNull] TState state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the unique identifier associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the unique identifier associated with the authorization.
        /// </returns>
        ValueTask<string> GetIdAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the scopes associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the scopes associated with the specified authorization.
        /// </returns>
        ValueTask<ImmutableArray<string>> GetScopesAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the status associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the status associated with the specified authorization.
        /// </returns>
        ValueTask<string> GetStatusAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the subject associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the subject associated with the specified authorization.
        /// </returns>
        ValueTask<string> GetSubjectAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the type associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the asynchronous operation,
        /// whose result returns the type associated with the specified authorization.
        /// </returns>
        ValueTask<string> GetTypeAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether the specified scopes are included in the authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="scopes">The scopes.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the scopes are included in the authorization, <c>false</c> otherwise.</returns>
        ValueTask<bool> HasScopesAsync([NotNull] object authorization, ImmutableArray<string> scopes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a given authorization is ad hoc.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization is ad hoc, <c>false</c> otherwise.</returns>
        ValueTask<bool> IsAdHocAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a given authorization is permanent.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization is permanent, <c>false</c> otherwise.</returns>
        ValueTask<bool> IsPermanentAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a given authorization has been revoked.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization has been revoked, <c>false</c> otherwise.</returns>
        ValueTask<bool> IsRevokedAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a given authorization is valid.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns><c>true</c> if the authorization is valid, <c>false</c> otherwise.</returns>
        ValueTask<bool> IsValidAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the specified query and returns all the corresponding elements.
        /// </summary>
        /// <param name="count">The number of results to return.</param>
        /// <param name="offset">The number of results to skip.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>All the elements returned when executing the specified query.</returns>
        IAsyncEnumerable<object> ListAsync(
            [CanBeNull] int? count = null, [CanBeNull] int? offset = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the specified query and returns all the corresponding elements.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="query">The query to execute.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>All the elements returned when executing the specified query.</returns>
        IAsyncEnumerable<TResult> ListAsync<TResult>(
            [NotNull] Func<IQueryable<object>, IQueryable<TResult>> query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the specified query and returns all the corresponding elements.
        /// </summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="query">The query to execute.</param>
        /// <param name="state">The optional state.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>All the elements returned when executing the specified query.</returns>
        IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
            [NotNull] Func<IQueryable<object>, TState, IQueryable<TResult>> query,
            [CanBeNull] TState state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Populates the specified descriptor using the properties exposed by the authorization.
        /// </summary>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask PopulateAsync([NotNull] OpenIddictAuthorizationDescriptor descriptor, [NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Populates the authorization using the specified descriptor.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask PopulateAsync([NotNull] object authorization, [NotNull] OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the authorizations that are marked as invalid and the ad-hoc ones that have no valid/nonexpired token attached.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask PruneAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes an authorization.
        /// </summary>
        /// <param name="authorization">The authorization to revoke.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
        ValueTask RevokeAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the application identifier associated with an authorization.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="identifier">The unique identifier associated with the client application.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask SetApplicationIdAsync([NotNull] object authorization, [CanBeNull] string identifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing authorization.
        /// </summary>
        /// <param name="authorization">The authorization to update.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask UpdateAsync([NotNull] object authorization, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing authorization.
        /// </summary>
        /// <param name="authorization">The authorization to update.</param>
        /// <param name="descriptor">The descriptor used to update the authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>
        /// A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.
        /// </returns>
        ValueTask UpdateAsync([NotNull] object authorization, [NotNull] OpenIddictAuthorizationDescriptor descriptor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the authorization to ensure it's in a consistent state.
        /// </summary>
        /// <param name="authorization">The authorization.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The validation error encountered when validating the authorization.</returns>
        IAsyncEnumerable<ValidationResult> ValidateAsync([NotNull] object authorization, CancellationToken cancellationToken = default);
    }
}
