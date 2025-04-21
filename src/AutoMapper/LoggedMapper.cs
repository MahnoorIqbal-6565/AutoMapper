namespace AutoMapper;
using System;
public class LoggedMapper : IMapper
{
    private readonly IMapper _mapper;
    private readonly string _logFilePath = "mapping.log"; 

    public LoggedMapper(IMapper mapper)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }
    private void LogOperation(string operation, Type sourceType, Type destinationType)
    {
        string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {operation} from {sourceType?.FullName} to {destinationType?.FullName}";
        System.IO.File.AppendAllText(_logFilePath, message + Environment.NewLine);
    }
    public TDestination Map<TDestination>(object source)
    {
        LogOperation("Map (object)", source?.GetType(), typeof(TDestination));
        return _mapper.Map<TDestination>(source);
    }
    public TDestination Map<TDestination>(object source, Action<IMappingOperationOptions<object, TDestination>> opts)
    {
        LogOperation("Map (object, opts)", source?.GetType(), typeof(TDestination));
        return _mapper.Map<TDestination>(source, opts);
    }
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        LogOperation("Map", typeof(TSource), typeof(TDestination));
        return _mapper.Map<TSource, TDestination>(source);
    }
    public TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> opts)
    {
        LogOperation("Map (opts)", typeof(TSource), typeof(TDestination));
        return _mapper.Map<TSource, TDestination>(source, opts);
    }
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        LogOperation("Map (into)", typeof(TSource), typeof(TDestination));
        return _mapper.Map(source, destination);
    }
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> opts)
    {
        LogOperation("Map (into, opts)", typeof(TSource), typeof(TDestination));
        return _mapper.Map(source, destination, opts);
    }
    public object Map(object source, Type sourceType, Type destinationType)
    {
        LogOperation("Map (types)", sourceType, destinationType);
        return _mapper.Map(source, sourceType, destinationType);
    }
    public object Map(object source, Type sourceType, Type destinationType, Action<IMappingOperationOptions> opts)
    {
        LogOperation("Map (types, opts)", sourceType, destinationType);
        return _mapper.Map(source, sourceType, destinationType, opts);
    }
    public object Map(object source, object destination, Type sourceType, Type destinationType)
    {
        LogOperation("Map (into, types)", sourceType, destinationType);
        return _mapper.Map(source, destination, sourceType, destinationType);
    }
    public object Map(object source, object destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions> opts)
    {
        LogOperation("Map (into, types, opts)", sourceType, destinationType);
        return _mapper.Map(source, destination, sourceType, destinationType, opts);
    }
    public IConfigurationProvider ConfigurationProvider => _mapper.ConfigurationProvider;
    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, System.Linq.Expressions.Expression<Func<TDestination, object>>[] membersToExpand,object parameters = null)

    {
        LogOperation("ProjectTo", source?.ElementType, typeof(TDestination));
        return _mapper.ProjectTo<TDestination>(source, parameters, membersToExpand);
    }
    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, System.Collections.Generic.IDictionary<string, object> parameters, params string[] membersToExpand)
    {
        LogOperation("ProjectTo (params)", source?.ElementType, typeof(TDestination));
        return _mapper.ProjectTo<TDestination>(source, parameters, membersToExpand);
    }
    public IQueryable ProjectTo(IQueryable source, Type destinationType, System.Collections.Generic.IDictionary<string, object> parameters = null, params string[] membersToExpand)
    {
        LogOperation("ProjectTo (type)", source?.ElementType, destinationType);
        return _mapper.ProjectTo(source, destinationType, parameters, membersToExpand);
    }
    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object parameters = null, params System.Linq.Expressions.Expression<Func<TDestination, object>>[] membersToExpand)
    {
        LogOperation("ProjectTo (params Expression[])", source?.ElementType, typeof(TDestination));
        return _mapper.ProjectTo<TDestination>(source, parameters, membersToExpand);
    }
    public object Map(object source, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts)
    {
        throw new NotImplementedException();
    }
    public object Map(object source, object destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts)
    {
        throw new NotImplementedException();
    }
}