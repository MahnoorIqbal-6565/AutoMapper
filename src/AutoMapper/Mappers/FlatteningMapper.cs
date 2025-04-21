using AutoMapper.Internal.Mappers;
namespace AutoMapper.Extensions
{
    public class FlatteningMapper : IObjectMapper
    {
        private static readonly MethodInfo DictionaryAddMethod = typeof(IDictionary<string, object>).GetMethod("Add", new[] { typeof(string), typeof(object) });
        private static readonly MethodInfo DictionaryContainsKeyMethod = typeof(IDictionary<string, object>).GetMethod("ContainsKey", new[] { typeof(string) });


        public bool IsMatch(TypePair context)
        {
          
            return (IsComplexObject(context.SourceType) && IsDictionary(context.DestinationType)) ||
                   (IsDictionary(context.SourceType) && IsComplexObject(context.DestinationType));
        }

        public Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression)
        {
            if (IsComplexObject(sourceExpression.Type) && IsDictionary(destExpression.Type))
            {
                return MapToDictionary(configuration, sourceExpression, destExpression);
            }
            else if (IsDictionary(sourceExpression.Type) && IsComplexObject(destExpression.Type))
            {
                return MapFromDictionary(configuration, sourceExpression, destExpression);
            }
            else
            {
                return sourceExpression;
            }
        }

        private static Expression MapToDictionary(IGlobalConfiguration configuration, Expression sourceExpression, Expression destExpression)
        {
            var (variables, expressions) = configuration.Scratchpad();
            var dictionary = destExpression; 
            var sourceType = sourceExpression.Type;

            
            var queue = new Queue<(Expression, string, Type)>();
            queue.Enqueue((sourceExpression, "", sourceType)); 

            while (queue.Count > 0)
            {
                var (currentSource, currentPath, currentType) = queue.Dequeue();

                if (currentType.IsPrimitive || currentType.IsValueType || currentType == typeof(string))
                {
                   
                    if (string.IsNullOrEmpty(currentPath)) continue; 
                    var keyExpression = Constant(currentPath);
                    var valueExpression = ToType(currentSource, typeof(object)); 
                    expressions.Add(
                        Expression.Call(dictionary, DictionaryAddMethod, keyExpression, valueExpression)
                    );
                }
                else if (IsEnumerableType(currentType) && currentType != typeof(string))
                {
                    
                    var elementType = currentType.GetElementType();
                    if (elementType == null) continue; 

                    var collectionVariable = Variable(currentType, "collection");
                    variables.Add(collectionVariable);
                    expressions.Add(Assign(collectionVariable, currentSource));

                    var indexVariable = Variable(typeof(int), "index");
                    variables.Add(indexVariable);
                    expressions.Add(Assign(indexVariable, Constant(0)));

                    var itemVariable = Variable(elementType, "item");
                    variables.Add(itemVariable);

                    var breakLabel = Label("loopBreak");
                    var getItemMethod = currentType.GetMethod("get_Item", new[] { typeof(int) }); 
                    var loop = Loop(
                        
                        IfThenElse(
                            LessThan(indexVariable, Expression.Property(collectionVariable, "Count")),
                            Block(
                         
                                Assign(itemVariable, ToType(
                                    Expression.Call(collectionVariable, getItemMethod, indexVariable), elementType)
                                ),
                            
                                MapToDictionary(configuration, itemVariable, dictionary, currentPath + "[" + indexVariable + "]"),
                                //increment the index
                                PostIncrementAssign(indexVariable)
                            ),
                            Break(breakLabel) 
                        ),
                        breakLabel
                    );
                    expressions.Add(loop);

                }
                else
                {
                    
                    foreach (var property in currentType.GetProperties())
                    {
                        var propertyExpression = Expression.Property(currentSource, property);
                        var newPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}.{property.Name}";
                        queue.Enqueue((propertyExpression, newPath, property.PropertyType));
                    }
                }
            }
            return Block(variables, expressions);
        }

        private static Expression MapToDictionary(IGlobalConfiguration configuration, Expression sourceExpression, Expression destExpression, string prefix)
        {
            var (variables, expressions) = configuration.Scratchpad();
            var dictionary = destExpression;
            var sourceType = sourceExpression.Type;


            if (sourceType.IsPrimitive || sourceType.IsValueType || sourceType == typeof(string))
            {
                
                var keyExpression = Constant(prefix);
                var valueExpression = ToType(sourceExpression, typeof(object)); 
                expressions.Add(
                    Expression.Call(dictionary, DictionaryAddMethod, keyExpression, valueExpression)
                );
                return Block(variables, expressions);
            }
            else if (IsEnumerableType(sourceType) && sourceType != typeof(string))
            {
                
                var elementType = sourceType.GetElementType();
                if (elementType == null) return Empty();

                var collectionVariable = Variable(sourceType, "collection");
                variables.Add(collectionVariable);
                expressions.Add(Assign(collectionVariable, sourceExpression));

                var indexVariable = Variable(typeof(int), "index");
                variables.Add(indexVariable);
                expressions.Add(Assign(indexVariable, Constant(0)));
                var itemVariable = Variable(elementType, "item");
                variables.Add(itemVariable);

                var breakLabel = Label("LoopBreak");
                var getItemMethod = sourceType.GetMethod("get_Item", new[] { typeof(int) });  // Get the MethodInfo
                var loop = Loop(
                    
                    IfThenElse(
                        LessThan(indexVariable, Expression.Property(collectionVariable, "Count")),
                        Block(
                            
                            Assign(itemVariable, ToType(
                                Expression.Call(collectionVariable, getItemMethod, indexVariable), elementType)
                            ),
                            
                            MapToDictionary(configuration, itemVariable, dictionary, prefix + "[" + indexVariable + "]"),
                            
                            PostIncrementAssign(indexVariable)
                        ),
                        Break(breakLabel) 
                    ),
                    breakLabel
                );
                expressions.Add(loop);
                return Block(variables, expressions);
            }
            else
            {

                foreach (var property in sourceType.GetProperties())
                {
                    var propertyExpression = Expression.Property(sourceExpression, property);
                    var newPrefix = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    expressions.Add(MapToDictionary(configuration, propertyExpression, dictionary, newPrefix));
                }
                return Block(variables, expressions);
            }
        }

        private static Expression MapFromDictionary(IGlobalConfiguration configuration, Expression sourceExpression, Expression destExpression)
        {
            var (variables, expressions) = configuration.Scratchpad();
            var dictionary = sourceExpression; 
            var destinationType = destExpression.Type;
            var destination = destExpression;

            if (destinationType.IsAbstract || destinationType.IsInterface)
            {
                return destExpression;
            }
            // Create a new instance of the destination type
            if (destExpression.NodeType == ExpressionType.Default)
            {
                destination = ObjectFactory.GenerateConstructorExpression(destinationType, configuration);
            }

            variables.Add(destination as ParameterExpression);

            foreach (var property in destinationType.GetProperties())
            {
                var propertyName = property.Name;
                var keyExpression = Constant(propertyName);
                var containsKeyExpression = Expression.Call(dictionary, DictionaryContainsKeyMethod, keyExpression);
                var propertyExpression = Expression.Property(destination, property);
                var dictionaryValueExpression = Expression.Convert(Expression.Property(dictionary, "Item", keyExpression), property.PropertyType);

                //check if the dictionary contains the key, and then assign.
                var assignment =
                    Expression.IfThen(
                        containsKeyExpression,
                        Assign(propertyExpression, dictionaryValueExpression)
                    );
                expressions.Add(assignment);
            }
            expressions.Add(destination);
            return Block(variables, expressions);
        }

        private static bool IsDictionary(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
                   type.GetGenericArguments()[0] == typeof(string) && type.GetGenericArguments()[1] == typeof(object);
        }

        private static bool IsComplexObject(Type type)
        {
            return !type.IsPrimitive && !type.IsValueType && type != typeof(string);
        }

        private static bool IsEnumerableType(Type type)
        {
            return type.GetInterfaces().Any(i => i == typeof(System.Collections.IEnumerable));
        }
    }
}
