namespace AutoMapper.Mappers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using AutoMapper.Internal;
    using AutoMapper.Internal.Mappers;

    public class CustomFormatMapper : IObjectMapper
    {
        private readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _formatConfigurations = new();

        public CustomFormatMapper(Action<CustomFormatMappingExpression> configure)
        {
            var expression = new CustomFormatMappingExpression();
            configure(expression);
            _formatConfigurations = expression.Configurations;
        }

        public bool IsMatch(TypePair typePair)
        {
            return _formatConfigurations.ContainsKey(typePair.SourceType);
        }
        public object Map(object source, object destination, ResolutionContext context)
        {
            var sourceType = source?.GetType();
            var destinationType = destination?.GetType() ?? context.DestinationType;
            if (sourceType != null && _formatConfigurations.ContainsKey(sourceType))
            {
                var sourceData = ParseCustomFormat(source);
                var config = _formatConfigurations[sourceType];
                foreach (var destProperty in config)
                {
                    if (sourceData.TryGetValue(destProperty.Key, out var sourceValue))
                    {
                        object convertedValue = context.Mapper.Map(sourceValue, null, sourceValue?.GetType(), destProperty.Value.PropertyType);
                        destProperty.Value.SetValue(destination, convertedValue);
                    }
                }
                return destination;
            }
            return context.Mapper.Map(source, destination, sourceType, destinationType);
        }

       public Expression MapExpression(IGlobalConfiguration configurationProvider, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression)
{
    if (!_formatConfigurations.TryGetValue(sourceExpression.Type, out var config))
    {
        return null; 
    }

    var parsedSourceDataParameter = Expression.Variable(typeof(Dictionary<string, string>), "parsedSourceData");
    var tryGetValueResultParameter = Expression.Variable(typeof(string), "value"); // Variable for the out parameter
    var tryGetValueMethodInfo = typeof(Dictionary<string, string>).GetMethod("TryGetValue", new[] { typeof(string), typeof(string).MakeByRefType() });

    var parseMethodInfo = GetType().GetMethod(nameof(ParseCustomFormatExpression), BindingFlags.Static | BindingFlags.NonPublic);
    var parsedSourceDataAssignment = Expression.Assign(parsedSourceDataParameter, Expression.Convert(Expression.Invoke(Expression.Constant(parseMethodInfo), sourceExpression), typeof(Dictionary<string, string>)));

    var assignments = new List<Expression>();

    foreach (var destPropertyConfig in config)
    {
        var destinationProperty = destExpression.Type.GetProperty(destPropertyConfig.Value.Name);
        if (destinationProperty != null && destinationProperty.CanWrite)
        {
            var tryGetValueCall = Expression.Call(parsedSourceDataParameter, tryGetValueMethodInfo, Expression.Constant(destPropertyConfig.Key), tryGetValueResultParameter);
            var ifTryGetValue = Expression.IfThen(
                tryGetValueCall, // Condition: TryGetValue returns true
                Expression.Assign(
                    Expression.Property(destExpression, destinationProperty),
                    Expression.Convert(tryGetValueResultParameter, destinationProperty.PropertyType)
                )
            );
            assignments.Add(ifTryGetValue);
        }
    }

    if (assignments.Any())
    {
        return Expression.Block(new[] { parsedSourceDataParameter, tryGetValueResultParameter }, parsedSourceDataAssignment, Expression.Block(assignments));
    }

    return null;
}        private Dictionary<string, string> ParseCustomFormat(object source)
        {
            if (source is Dictionary<string, string> data)
            {
                return data;
            }
            throw new InvalidOperationException("Source object is not in the expected custom format (Dictionary<string, string> for this example).");
        }
        private static Expression ParseCustomFormatExpression(object source)
        {
            return Expression.Convert(Expression.Constant(source), typeof(Dictionary<string, string>));
        }
    }

    public class CustomFormatMappingExpression
    {
        internal Dictionary<Type, Dictionary<string, PropertyInfo>> Configurations { get; } = new();

        public void CreateMap<TSourceFormat, TDestination>(Action<CustomFormatMemberConfiguration<TSourceFormat, TDestination>> memberConfig)
        {
            var config = new Dictionary<string, PropertyInfo>();
            var memberExpression = new CustomFormatMemberConfiguration<TSourceFormat, TDestination>(config);
            memberConfig(memberExpression);
            Configurations[typeof(TSourceFormat)] = config;
        }
    }

    public class CustomFormatMemberConfiguration<TSourceFormat, TDestination>
    {
        private readonly Dictionary<string, PropertyInfo> _config;

        public CustomFormatMemberConfiguration(Dictionary<string, PropertyInfo> config)
        {
            _config = config;
        }

        public void MapMember(string sourceField, System.Linq.Expressions.Expression<Func<TDestination, object>> destinationMember)
        {
            if (destinationMember.Body is MemberExpression memberExpression && memberExpression.Member is PropertyInfo propertyInfo)
            {
                _config[sourceField] = propertyInfo;
            }
            else
            {
                throw new ArgumentException("Destination member must be a property.");
            }
        }
    }
}