
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using JetBrains.Annotations;

namespace BagoumLib {
/// <summary>
/// A store that allows registering and locating services at runtime.
/// </summary>
[PublicAPI]
public static class ServiceLocator {
    private interface IService {
        public void VerifyOptions(ServiceOptions nOptions);
    }

    /// <summary>
    /// Options configuring the usage of a specific service type that may be realized by multiple providers.
    /// </summary>
    public record struct ServiceOptions() {
        /// <summary>
        /// True iff at most one provider can realize the service at a time. Default false.
        /// </summary>
        public bool Unique { get; init; } = false;
    }
    
    private class Service<T>(ServiceOptions? options) : IService
        where T : class {
        private readonly DMCompactingArray<T> providers = new();
        private readonly ServiceOptions options = options ?? new ServiceOptions();

        public void VerifyOptions(ServiceOptions nOptions) {
            if (options != nOptions)
                throw new Exception($"Multiple providers have given different option configurations for the service: existing {options}; new {nOptions}");
        }

        public IDisposable Add(T service) {
            providers.Compact();
            if (options.Unique && providers.Count > 0)
                throw new Exception($"An instance of unique service {typeof(T)} already exists.");
            return providers.Add(service);
        }

        public T? FindOrNull() => providers.FirstOrNull();
        public Maybe<T> MaybeFind() => providers.FirstOrNone();

        public IReadOnlyDMCompactingArray<T> FindAll() => providers;
    }

    private static readonly Dictionary<Type, IService> services = new();

    /// <summary>
    /// Register a service that can be reached globally via service location.
    /// <br/>The caller must dispose the disposable when the registered service is no longer available
    ///  (eg. due to object deletion).
    /// <br/>TODO send alerts to consumers when the service is disposed
    /// </summary>
    public static IDisposable Register<T>(T provider, ServiceOptions? options = null) where T : class {
        if (!services.TryGetValue(typeof(T), out var s))
            s = services[typeof(T)] = new Service<T>(options);
        else if (options is {} o)
            s.VerifyOptions(o);
        return ((Service<T>) s).Add(provider);
    }
    
    /// <summary>
    /// Find a service of type T, or return null.
    /// </summary>
    public static T? FindOrNull<T>() where T : class =>
        services.TryGetValue(typeof(T), out var s) ? 
            ((Service<T>) s).FindOrNull() : 
            null;
    
    /// <summary>
    /// Find a service of type T, or return null.
    /// </summary>
    public static Maybe<T> MaybeFind<T>() where T : class =>
        services.TryGetValue(typeof(T), out var s) ? 
            ((Service<T>) s).MaybeFind() : 
            Maybe<T>.None;

    /// <summary>
    /// Find all services of type T. The returned array may be empty.
    /// </summary>
    public static IReadOnlyDMCompactingArray<T> FindAll<T>() where T : class =>
        services.TryGetValue(typeof(T), out var s) ? 
            ((Service<T>) s).FindAll() : 
            DMCompactingArray<T>.EmptyArray;

    /// <summary>
    /// Find a service of type T, or throw an exception.
    /// </summary>
    public static T Find<T>() where T : class =>
        FindOrNull<T>() ?? throw new Exception($"Service locator: No provider of type {typeof(T)} found");
}
}
