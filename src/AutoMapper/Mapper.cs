namespace AutoMapper;

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;

// Removed: IObjectMappingOperationOptions - No more mapping options

// Removed: Factory delegate - Users will create services directly if needed
// using Factory = Func<Type, object>;

// Simplified interfaces - No more IMapperBase or IRuntimeMapper
public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    object Map(object source, Type sourceType, Type destinationType);
    object Map(object source, object destination, Type sourceType, Type destinationType);

    // Removed: IConfigurationProvider - Configuration is handled directly
    // IConfigurationProvider ConfigurationProvider { get; }

    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object parameters = null, params Expression<Func<TDestination, object>>[] membersToExpand);
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters, params string[] membersToExpand);
    IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object> parameters = null, params string[] membersToExpand);
}

// Removed: IRuntimeMapper - Internal runtime mapping is merged into Mapper
// public interface IRuntimeMapper : IMapperBase
// {
// }

// Removed: IInternalRuntimeMapper - Internal runtime mapping is now part of Mapper
// internal interface IInternalRuntimeMapper : IRuntimeMapper
// {
//  TDestination Map<TSource, TDestination>(TSource source, TDestination destination, ResolutionContext context, Type sourceType = null, Type destinationType = null, MemberMap memberMap = null);
//  ResolutionContext DefaultContext { get; }
//  Factory ServiceCtor { get; }
// }

public sealed class Mapper : IMapper // No longer implements IInternalRuntimeMapper
{
    private readonly IGlobalConfiguration _configuration; // Direct dependency - No more Strategy
    // private readonly Factory _serviceCtor; // Removed - No more Factory
    private readonly ResolutionContext _defaultContext;

    // Constructor now takes IGlobalConfiguration directly
    public Mapper(IGlobalConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        _configuration = configuration;
        // _serviceCtor = serviceCtor; // Removed
        _defaultContext = new ResolutionContext(this, null); // Simplified context creation
    }

    // Removed: ResolutionContext property - Context is used internally
    // ResolutionContext IInternalRuntimeMapper.DefaultContext => _defaultContext;

    // Removed: Factory property
    // Factory IInternalRuntimeMapper.ServiceCtor => _serviceCtor;

    // Now a direct property, not from an interface
    public IGlobalConfiguration ConfigurationProvider => _configuration;

    public TDestination Map<TDestination>(object source) => Map(source, default(TDestination));

    public TDestination Map<TSource, TDestination>(TSource source) => Map(source, default(TDestination));

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination) =>
        MapCore(source, destination, _defaultContext); // Pass the default context

    public object Map(object source, Type sourceType, Type destinationType) =>
        MapCore(source, null, _defaultContext, sourceType, destinationType);

    public object Map(object source, object destination, Type sourceType, Type destinationType) =>
        MapCore(source, destination, _defaultContext, sourceType, destinationType);

    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object parameters = null, params Expression<Func<TDestination, object>>[] membersToExpand) =>
        source.ProjectTo(ConfigurationProvider, parameters, membersToExpand);

    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters, params string[] membersToExpand) =>
        source.ProjectTo<TDestination>(ConfigurationProvider, parameters, membersToExpand);

    public IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object> parameters, params string[] membersToExpand) =>
        source.ProjectTo(destinationType, ConfigurationProvider, parameters, membersToExpand);

    // Combined Map methods, no more options
    private TDestination MapCore<TSource, TDestination>(
        TSource source, TDestination destination, ResolutionContext context, Type sourceType = null, Type destinationType = null, MemberMap memberMap = null)
    {
        TypePair requestedTypes = new(typeof(TSource), typeof(TDestination));
        TypePair runtimeTypes = new(source?.GetType() ?? sourceType ?? typeof(TSource), destination?.GetType() ?? destinationType ?? typeof(TDestination));
        MapRequest mapRequest = new(requestedTypes, runtimeTypes, memberMap);

        // Direct call to the execution plan - No more Strategy
        var executionPlan = _configuration.GetExecutionPlan<TSource, TDestination>(mapRequest);
        return executionPlan(source, destination, context);
    }
}

// Removed: ResolutionContext and other internal classes are assumed to be directly used
// by the mapping execution plan.  For brevity, they are not included here.