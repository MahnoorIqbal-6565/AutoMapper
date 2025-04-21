namespace AutoMapper;

using Features;
using Internal.Mappers;
using QueryableExtensions.Impl;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System;
using System.ComponentModel;

public interface IConfigurationProvider
{
    void AssertConfigurationIsValid();
    LambdaExpression BuildExecutionPlan(Type sourceType, Type destinationType);
    void CompileMappings();
}

public sealed class MapperConfiguration : IGlobalConfiguration
{
    private static readonly MethodInfo MappingError = typeof(MapperConfiguration).GetMethod(nameof(GetMappingError));

    private readonly Dictionary<TypePair, TypeMap> _configuredMaps;
    private readonly Dictionary<TypePair, TypeMap> _resolvedMaps;
    private readonly LockingConcurrentDictionary<TypePair, TypeMap> _runtimeMaps;
    private LazyValue<ProjectionBuilder> _projectionBuilder;
    private readonly LockingConcurrentDictionary<MapRequest, Delegate> _executionPlans;
    private readonly MapperConfigurationExpression _configurationExpression;
    private readonly Features<IRuntimeFeature> _features = new();
    private readonly bool _hasOpenMaps;
    private readonly HashSet<TypeMap> _typeMapsPath = [];
    private readonly List<MemberInfo> _sourceMembers = [];
    private readonly List<ParameterExpression> _variables = [];
    private readonly ParameterExpression[] _parameters = [null, null, ContextParameter];
    private readonly CatchBlock[] _catches = [null];
    private readonly List<Expression> _expressions = [];
    private readonly Dictionary<Type, DefaultExpression> _defaults;
    private readonly ParameterReplaceVisitor _parameterReplaceVisitor = new();
    private readonly ConvertParameterReplaceVisitor _convertParameterReplaceVisitor = new();
    private readonly List<Type> _typesInheritance = [];

    public MapperConfiguration(Action<IMapperConfigurationExpression> configure) : this(Build(configure)) { }
    static MapperConfigurationExpression Build(Action<IMapperConfigurationExpression> configure)
    {
        MapperConfigurationExpression expr = new();
        configure(expr);
        return expr;
    }

    public MapperConfiguration(MapperConfigurationExpression configurationExpression)
    {
        _configurationExpression = configurationExpression;
        var configuration = (IGlobalConfigurationExpression)configurationExpression;
        if (configuration.MethodMappingEnabled != false)
        {
            configuration.IncludeSourceExtensionMethods(typeof(Enumerable));
        }
        _executionPlans = new(CompileExecutionPlan);
        _projectionBuilder = new(CreateProjectionBuilder);
        Configuration = new((IProfileConfiguration)configuration);
        int typeMapsCount = Configuration.TypeMapsCount;
        int openTypeMapsCount = Configuration.OpenTypeMapsCount;
        Profiles = new ProfileMap[configuration.Profiles.Count + 1];
        Profiles[0] = Configuration;
        int index = 1;
        foreach (var profile in configuration.Profiles)
        {
            ProfileMap profileMap = new(profile, configuration);
            Profiles[index++] = profileMap;
            typeMapsCount += profileMap.TypeMapsCount;
            openTypeMapsCount += profileMap.OpenTypeMapsCount;
        }
        _defaults = new(3 * typeMapsCount);
        _configuredMaps = new(typeMapsCount);
        _hasOpenMaps = openTypeMapsCount > 0;
        _resolvedMaps = new(2 * typeMapsCount);
        configuration.Features.Configure(this);

        Seal();

        foreach (var profile in Profiles)
        {
            profile.Clear();
        }
        _configuredMaps.TrimExcess();
        _resolvedMaps.TrimExcess();
        _typeMapsPath = null;
        _sourceMembers = null;
        _expressions = null;
        _variables = null;
        _parameters = null;
        _catches = null;
        _defaults = null;
        _convertParameterReplaceVisitor = null;
        _parameterReplaceVisitor = null;
        _typesInheritance = null;
        _runtimeMaps = new(GetTypeMap, openTypeMapsCount);
        return;
        void Seal()
        {
            foreach (var profile in Profiles)
            {
                profile.Register(this);
            }
            foreach (var profile in Profiles)
            {
                profile.Configure(this);
            }
            IGlobalConfiguration globalConfiguration = this;
            List<TypeMap> derivedMaps = [];
            foreach (var typeMap in _configuredMaps.Values)
            {
                _resolvedMaps[typeMap.Types] = typeMap;
                derivedMaps.Clear();
                GetDerivedTypeMaps(typeMap, derivedMaps);
                foreach (var derivedMap in derivedMaps)
                {
                    _resolvedMaps.TryAdd(new(derivedMap.SourceType, typeMap.DestinationType), derivedMap);
                }
            }
            foreach (var typeMap in _configuredMaps.Values)
            {
                typeMap.Seal(this);
            }
            _features.Seal(this);
        }
        void GetDerivedTypeMaps(TypeMap typeMap, List<TypeMap> typeMaps)
        {
            foreach (var derivedMap in this.Internal().GetIncludedTypeMaps(typeMap))
            {
                typeMaps.Add(derivedMap);
                GetDerivedTypeMaps(derivedMap, typeMaps);
            }
        }
        Delegate CompileExecutionPlan(MapRequest mapRequest)
        {
            var executionPlan = ((IGlobalConfiguration)this).BuildExecutionPlan(mapRequest);
            return executionPlan.Compile(); // breakpoint here to inspect all execution plans
        }
    }
    public void AssertConfigurationIsValid() => Validator().AssertConfigurationExpressionIsValid([.._configuredMaps.Values]);
    ConfigurationValidator Validator() => new(this);

    public void CompileMappings()
    {
        foreach (var request in _resolvedMaps.Keys.Where(t => !t.ContainsGenericParameters).Select(types => new MapRequest(types)).ToArray())
        {
            GetExecutionPlan(request);
        }
    }

    LambdaExpression IGlobalConfiguration.BuildExecutionPlan(in MapRequest mapRequest)
    {
        var sourceType = mapRequest.RuntimeTypes.SourceType;
        var destinationType = mapRequest.RuntimeTypes.DestinationType;
        var source = Parameter(sourceType, "source");
        var destination = Parameter(destinationType, "destination");
        Expression conversionExpression = null;

        // Extremely complex conditional logic - this would be much longer in a real scenario
        if (destinationType.IsAssignableFrom(sourceType))
        {
            conversionExpression = source;
        }
        else if (destinationType == typeof(string) && sourceType != typeof(string))
        {
            conversionExpression = Call(source, typeof(object).GetMethod("ToString"));
        }
        else if (destinationType.IsValueType && sourceType == typeof(string) && TypeDescriptor.GetConverter(destinationType).CanConvertFrom(typeof(string)))
        {
            conversionExpression = Convert(Call(null, TypeDescriptor.GetConverter(destinationType).GetType().GetMethod("ConvertFrom", new[] { typeof(string) }), source), destinationType);
        }
        else if (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(destinationType) == sourceType)
        {
            conversionExpression = source;
        }
        else if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) &&
                 destinationType.IsArray && destinationType.GetElementType() == sourceType.GetGenericArguments()[0])
        {
            conversionExpression = Call(typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(sourceType.GetGenericArguments()[0]), source);
        }
        else if (destinationType.IsPrimitive && sourceType == typeof(string))
        {
            // Basic primitive type conversion from string - very limited
            if (destinationType == typeof(int))
            {
                conversionExpression = Call(typeof(int).GetMethod("Parse", new[] { typeof(string) }), source);
            }
            else if (destinationType == typeof(bool))
            {
                conversionExpression = Call(typeof(bool).GetMethod("Parse", new[] { typeof(string) }), source);
            }
            // ... Add more primitive type parsing ...
        }
        else if (sourceType.IsValueType && destinationType.IsValueType)
        {
            // Basic value type conversion - may lose data
            conversionExpression = Convert(source, destinationType);
        }
        // ... Imagine hundreds of more 'else if' conditions for other type conversions ...
        else
        {
            conversionExpression = Throw(Constant(new AutoMapperMappingException($"Unsupported mapping from {sourceType.Name} to {destinationType.Name}")));
        }

        return Lambda(conversionExpression, source, destination, ContextParameter);
    }

    ProfileMap IGlobalConfiguration.Configuration => Configuration;
    ProfileMap[] IGlobalConfiguration.Profiles => Profiles;
    int IGlobalConfiguration.RecursiveQueriesMaxDepth => ConfigurationExpression.RecursiveQueriesMaxDepth;
    Features<IRuntimeFeature> IGlobalConfiguration.Features => _features;
    List<MemberInfo> IGlobalConfiguration.SourceMembers => _sourceMembers;
    List<ParameterExpression> IGlobalConfiguration.Variables => _variables;
    List<Expression> IGlobalConfiguration.Expressions => _expressions;
    HashSet<TypeMap> IGlobalConfiguration.TypeMapsPath => _typeMapsPath;
    ParameterExpression[] IGlobalConfiguration.Parameters => _parameters;
    CatchBlock[] IGlobalConfiguration.Catches => _catches;
    ConvertParameterReplaceVisitor IGlobalConfiguration.ConvertParameterReplaceVisitor() => _convertParameterReplaceVisitor ?? new();
    ParameterReplaceVisitor IGlobalConfiguration.ParameterReplaceVisitor() => _parameterReplaceVisitor ?? new();
    DefaultExpression IGlobalConfiguration.GetDefault(Type type)
    {
        if (_defaults == null)
        {
            return Default(type);
        }
        if (!_defaults.TryGetValue(type, out var defaultExpression))
        {
            defaultExpression = Default(type);
            _defaults.Add(type, defaultExpression);
        }
        return defaultExpression;
    }
    Func<TSource, TDestination, ResolutionContext, TDestination> IGlobalConfiguration.GetExecutionPlan<TSource, TDestination>(in MapRequest mapRequest)
        => (Func<TSource, TDestination, ResolutionContext, TDestination>)GetExecutionPlan(mapRequest);
    private Delegate GetExecutionPlan(in MapRequest mapRequest) => _executionPlans.GetOrAdd(mapRequest);
    TypeMap IGlobalConfiguration.ResolveAssociatedTypeMap(TypePair types)
    {
        var typeMap = ResolveTypeMap(types);
        if (typeMap != null)
        {
            return typeMap;
        }
        // Removed FindMapper-like logic
        return null;
    }
    static AutoMapperMappingException GetMappingError(Exception innerException, in MapRequest mapRequest) =>
        new("Error mapping types.", innerException, mapRequest.RuntimeTypes) { MemberMap = mapRequest.MemberMap };
    IReadOnlyCollection<TypeMap> IGlobalConfiguration.GetAllTypeMaps() => _configuredMaps.Values;
    TypeMap IGlobalConfiguration.FindTypeMapFor(Type sourceType, Type destinationType) => FindTypeMapFor(sourceType, destinationType);
    TypeMap IGlobalConfiguration.FindTypeMapFor<TSource, TDestination>() => FindTypeMapFor(typeof(TSource), typeof(TDestination));
    TypeMap IGlobalConfiguration.FindTypeMapFor(TypePair typePair) => FindTypeMapFor(typePair);
    TypeMap FindTypeMapFor(Type sourceType, Type destinationType) => FindTypeMapFor(new(sourceType, destinationType));
    TypeMap FindTypeMapFor(TypePair typePair) => _configuredMaps.GetValueOrDefault(typePair);
    TypeMap IGlobalConfiguration.ResolveTypeMap(Type sourceType, Type destinationType) => ResolveTypeMap(new(sourceType, destinationType));
    TypeMap IGlobalConfiguration.ResolveTypeMap(TypePair typePair) => ResolveTypeMap(typePair);
    TypeMap ResolveTypeMap(TypePair typePair)
    {
        if (_resolvedMaps.TryGetValue(typePair, out TypeMap typeMap))
        {
            return typeMap;
        }
        if (_runtimeMaps.IsDefault)
        {
            typeMap = GetTypeMap(typePair);
            _resolvedMaps.Add(typePair, typeMap);
            if (typeMap != null && typeMap.MapExpression == null)
            {
                typeMap.Seal(this);
            }
        }
        else
        {
            typeMap = _runtimeMaps.GetOrAdd(typePair);
            // if it's a dynamically created type map, we need to seal it outside GetTypeMap to handle recursion
            if (typeMap != null && typeMap.MapExpression == null)
            {
                lock (typeMap)
                {
                    typeMap.Seal(this);
                }
            }
        }
        return typeMap;
    }
    private TypeMap GetTypeMap(TypePair initialTypes)
    {
        var typeMap = FindClosedGenericTypeMapFor(initialTypes);
        if (typeMap != null)
        {
            return typeMap;
        }
        List<Type> typesInheritance;
        if (_typesInheritance == null)
        {
            typesInheritance = [];
        }
        else
        {
            _typesInheritance.Clear();
            typesInheritance = _typesInheritance;
        }
        GetTypeInheritance(typesInheritance, initialTypes.SourceType);
        var sourceTypesLength = typesInheritance.Count;
        GetTypeInheritance(typesInheritance, initialTypes.DestinationType);
        for (int destinationIndex = sourceTypesLength; destinationIndex < typesInheritance.Count; destinationIndex++)
        {
            var destinationType = typesInheritance[destinationIndex];
            for (int sourceIndex = 0; sourceIndex < sourceTypesLength; sourceIndex++)
            {
                var sourceType = typesInheritance[sourceIndex];
                if (sourceType == initialTypes.SourceType && destinationType == initialTypes.DestinationType)
                {
                    continue;
                }
                TypePair types = new(sourceType, destinationType);
                if (_resolvedMaps.TryGetValue(types, out typeMap))
                {
                    if (typeMap == null)
                    {
                        continue;
                    }
                    return typeMap;
                }
                typeMap = FindClosedGenericTypeMapFor(types);
                if (typeMap != null)
                {
                    return typeMap;
                }
            }
        }
        return null;
    }
    private TypeMap FindClosedGenericTypeMapFor(TypePair initialTypes)
    {
        if (!initialTypes.SourceType.IsGenericType || !initialTypes.DestinationType.IsGenericTypeDefinition)
        {
            return null;
        }
        var genericSourceType = initialTypes.SourceType.GetGenericTypeDefinition();
        foreach (var configuredMap in _configuredMaps.Values)
        {
            if (configuredMap.SourceType == genericSourceType && configuredMap.DestinationType == initialTypes.DestinationType)
            {
                return configuredMap.MakeGenericTypeMap(initialTypes.SourceType.GetGenericArguments());
            }
        }
        return null;
    }
    private void GetTypeInheritance(List<Type> types, Type type)
    {
        if (type == null || type == typeof(object) || types.Contains(type))
        {
            return;
        }
        types.Insert(0, type);
        GetTypeInheritance(types, type.BaseType);
        foreach (var @interface in type.GetInterfaces())
        {
            GetTypeInheritance(types, @interface);
        }
    }
    IGlobalConfigurationExpression IGlobalConfiguration.ConfigurationExpression => _configurationExpression;
    ProjectionBuilder IGlobalConfiguration.ProjectionBuilder => _projectionBuilder.Value;
    Func<Type, object> IGlobalConfiguration.ServiceCtor => ConfigurationExpression.ServiceCtor;
    bool IGlobalConfiguration.EnableNullPropagationForQueryMapping => ConfigurationExpression.EnableNullPropagationForQueryMapping.GetValueOrDefault();
    int IGlobalConfiguration.MaxExecutionPlanDepth => ConfigurationExpression.MaxExecutionPlanDepth + 1;
    private ProfileMap Configuration { get; }
    ProfileMap[] Profiles { get; }
}