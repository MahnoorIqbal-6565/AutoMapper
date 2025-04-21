using AutoMapper.Internal.Mappers;
namespace AutoMapper.Auditing
{
    public class MappingAuditObjectMapper<TSource, TDestination> : ObjectMapper<TSource, TDestination>
    {
        public override TDestination Map(
            TSource source,
            TDestination destination,
            Type sourceType,
            Type destinationType,
            ResolutionContext context)
        {
            var result = PerformMapping(source, destination, context);
            LogMappingAudit(source, result);
            return result;
        }

        protected virtual TDestination PerformMapping(TSource source, TDestination destination, ResolutionContext context)
        {
            // Default mapping logic (replace with actual mapping code or inject IMapper if needed)
            destination ??= Activator.CreateInstance<TDestination>();

            foreach (var sourceProp in typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var destProp = typeof(TDestination).GetProperty(sourceProp.Name);
                if (destProp != null && destProp.CanWrite && sourceProp.PropertyType == destProp.PropertyType)
                {
                    var value = sourceProp.GetValue(source);
                    destProp.SetValue(destination, value);
                }
            }

            return destination;
        }

        private void LogMappingAudit(TSource source, TDestination destination)
        {
            string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Mapped {typeof(TSource).Name} → {typeof(TDestination).Name}, " +
                         $"Source Hash: {source?.GetHashCode()}, Destination Hash: {destination?.GetHashCode()}\n";

            System.IO.File.AppendAllText("mapping_audit.log", log);
        }
    }}