using AutoMapper;

public class ValidationMapper : IMapper
{
    private readonly IMapper _innerMapper;

    public ValidationMapper(IMapper innerMapper)
    {
        _innerMapper = innerMapper;
    }

    public IConfigurationProvider ConfigurationProvider => _innerMapper.ConfigurationProvider;

    public TDestination Map<TDestination>(object source)
    {
        Validate(source, typeof(TDestination));
        return _innerMapper.Map<TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        Validate(source, typeof(TSource), typeof(TDestination));
        return _innerMapper.Map<TSource, TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        Validate(source, typeof(TSource), typeof(TDestination));
        return _innerMapper.Map(source, destination);
    }

    public object Map(object source, Type sourceType, Type destinationType)
    {
        Validate(source, sourceType, destinationType);
        return _innerMapper.Map(source, sourceType, destinationType);
    }

    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        Validate(source, sourceType, destinationType);
        return _innerMapper.Map(source, destination, sourceType, destinationType);
    }

    public TDestination Map<TDestination>(object source, Action<IMappingOperationOptions<object, TDestination>> opts)
    {
        Validate(source, typeof(TDestination));
        return _innerMapper.Map(source, opts);
    }

    public TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> opts)
    {
        Validate(source, typeof(TSource), typeof(TDestination));
        return _innerMapper.Map(source, opts);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> opts)
    {
        Validate(source, typeof(TSource), typeof(TDestination));
        return _innerMapper.Map(source, destination, opts);
    }

    public object Map(object source, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts)
    {
        Validate(source, sourceType, destinationType);
        return _innerMapper.Map(source, sourceType, destinationType, opts);
    }

    public object Map(object source, object destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts)
    {
        Validate(source, sourceType, destinationType);
        return _innerMapper.Map(source, destination, sourceType, destinationType, opts);
    }

    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object parameters = null, params Expression<Func<TDestination, object>>[] membersToExpand)
    {
        return _innerMapper.ProjectTo<TDestination>(source, parameters, membersToExpand);
    }

    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters, params string[] membersToExpand)
    {
        return _innerMapper.ProjectTo<TDestination>(source, parameters, membersToExpand);
    }

    public IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object> parameters = null, params string[] membersToExpand)
    {
        return _innerMapper.ProjectTo(source, destinationType, parameters, membersToExpand);
    }

    private void Validate(object source, Type sourceType, Type destinationType)
    {
        if (source == null) return;

        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destProps = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in destProps)
        {
            var sourceProp = sourceProps.FirstOrDefault(p => p.Name == prop.Name && p.PropertyType == prop.PropertyType);
            if (sourceProp != null)
            {
                var value = sourceProp.GetValue(source);
                if (value == null)
                {
                    throw new InvalidOperationException($"Validation failed: Property '{prop.Name}' on source cannot be null.");
                }
            }
        }
    }

    private void Validate(object source, Type destinationType) => Validate(source, source?.GetType(), destinationType);
}
