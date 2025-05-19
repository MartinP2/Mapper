using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MapperLibrary;

/// <summary>
/// A flexible mapper that can automatically map between different models, handling simple and complex properties.
/// </summary>
public class Mapper
{
    private readonly Dictionary<(Type sourceType, Type targetType), Delegate> _mapCache = new Dictionary<(Type sourceType, Type targetType), Delegate>();
    private readonly Dictionary<string, Dictionary<string, string>> _propertyMappings = new Dictionary<string, Dictionary<string, string>>();
    private readonly Dictionary<string, HashSet<string>> _ignoredProperties = new Dictionary<string, HashSet<string>>();

    /// <summary>
    /// Maps a source object to a new instance of the target type using expression trees.
    /// </summary>
    /// <typeparam name="TSource">The source type to map from</typeparam>
    /// <typeparam name="TTarget">The target type to map to</typeparam>
    /// <param name="source">The source object</param>
    /// <returns>A new instance of the target type with properties mapped from the source</returns>
    public TTarget Map<TSource, TTarget>(TSource source) where TTarget : new()
    {
        if (source == null)
            return default;

        var mapper = GetOrCreateMapFunction<TSource, TTarget>();
        return mapper(source);
    }

    /// <summary>
    /// Maps a collection of source objects to a collection of target objects.
    /// </summary>
    /// <typeparam name="TSource">The source type to map from</typeparam>
    /// <typeparam name="TTarget">The target type to map to</typeparam>
    /// <param name="sources">The collection of source objects</param>
    /// <returns>A collection of mapped target objects</returns>
    public IEnumerable<TTarget> MapCollection<TSource, TTarget>(IEnumerable<TSource> sources) where TTarget : new()
    {
        if (sources == null)
            return Enumerable.Empty<TTarget>();

        var mapper = GetOrCreateMapFunction<TSource, TTarget>();
        return sources.Select(mapper);
    }

    /// <summary>
    /// Creates a mapping configuration for specific types.
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <returns>A mapping configuration builder</returns>
    public MappingConfiguration<TSource, TTarget> CreateMap<TSource, TTarget>() where TTarget : new()
    {
        return new MappingConfiguration<TSource, TTarget>(this);
    }

    /// <summary>
    /// Creates or retrieves a mapping function using expression trees.
    /// </summary>
    private Func<TSource, TTarget> GetOrCreateMapFunction<TSource, TTarget>() where TTarget : new()
    {
        var sourceType = typeof(TSource);
        var targetType = typeof(TTarget);
        var key = (sourceType, targetType);

        if (_mapCache.TryGetValue(key, out var cachedMap))
            return (Func<TSource, TTarget>)cachedMap;

        var map = MapExpressions<TSource, TTarget>();
        _mapCache[key] = map;
        return map;
    }

    /// <summary>
    /// Creates a mapping function using expression trees to efficiently map between types.
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <returns>A compiled function that maps from source to target</returns>
    public Func<TSource, TTarget> MapExpressions<TSource, TTarget>() where TTarget : new()
    {
        var sourceType = typeof(TSource);
        var targetType = typeof(TTarget);
        
        // Parameter expression for the source object
        var sourceParam = Expression.Parameter(sourceType, "source");
        
        // Create a new instance of the target type
        var newTarget = Expression.New(targetType);
        
        // Get all the properties of the target type
        var targetProperties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);
          // Create property assignments for the target
        var bindings = new List<MemberBinding>();
        
        foreach (var targetProp in targetProperties)
        {
            // Skip ignored properties
            if (IsPropertyIgnored(targetType.Name, targetProp.Name))
                continue;
                
            var customMapping = GetCustomPropertyMapping(sourceType.Name, targetType.Name, targetProp.Name);
            var sourcePropName = customMapping ?? targetProp.Name;
            var sourceProp = sourceType.GetProperty(sourcePropName, BindingFlags.Public | BindingFlags.Instance);
            
            if (sourceProp != null)
            {
                Expression sourceValue = Expression.Property(sourceParam, sourceProp);
                
                // Handle different types of mappings
                if (NeedsTypeConversion(sourceProp.PropertyType, targetProp.PropertyType))
                {
                    sourceValue = CreateConversionExpression(sourceValue, sourceProp.PropertyType, targetProp.PropertyType, sourceParam);
                }
                
                bindings.Add(Expression.Bind(targetProp, sourceValue));
            }
        }
        
        // Create and initialize the target object
        var memberInit = Expression.MemberInit(newTarget, bindings);
        
        // Create a lambda expression and compile it to a function
        var lambda = Expression.Lambda<Func<TSource, TTarget>>(memberInit, sourceParam);
        return lambda.Compile();
    }

    /// <summary>
    /// Determines if type conversion is needed between source and target property types.
    /// </summary>
    private bool NeedsTypeConversion(Type sourceType, Type targetType)
    {
        // Check if types are directly assignable
        if (targetType.IsAssignableFrom(sourceType))
            return false;
        
        // Check for enum conversions
        bool sourceIsEnum = sourceType.IsEnum;
        bool targetIsEnum = targetType.IsEnum;
        if (sourceIsEnum && targetIsEnum)
            return true;
        
        // Check for collection conversions
        bool sourceIsList = IsGenericList(sourceType);
        bool targetIsList = IsGenericList(targetType);
        if (sourceIsList && targetIsList)
            return true;
        
        // Check for complex object conversions
        if (!sourceType.IsPrimitive && !targetType.IsPrimitive && 
            sourceType != typeof(string) && targetType != typeof(string))
            return true;
        
        // Check for primitive type conversions
        return true;
    }    /// <summary>
    /// Creates an expression to convert between different types.
    /// </summary>
    private Expression CreateConversionExpression(Expression sourceValue, Type sourceType, Type targetType, ParameterExpression sourceParam)
    {
        // Handle reference types that could be null
        Expression result;
        
        // If source is a reference type, handle null case
        if (!sourceType.IsValueType)
        {
            var sourceNotNull = Expression.NotEqual(sourceValue, Expression.Constant(null));
            
            // Handle enum conversion
            if (sourceType.IsEnum && targetType.IsEnum)
            {
                result = Expression.Convert(
                    Expression.Convert(sourceValue, typeof(int)),
                    targetType
                );
            }
            // Collection conversion
            else if (IsGenericList(sourceType) && IsGenericList(targetType))
            {
                var sourceElementType = sourceType.GetGenericArguments()[0];
                var targetElementType = targetType.GetGenericArguments()[0];
                
                // Create method call to MapCollection
                var mapMethod = typeof(Mapper).GetMethod("MapCollection").MakeGenericMethod(sourceElementType, targetElementType);
                var callMapCollection = Expression.Call(
                    Expression.Constant(this),
                    mapMethod,
                    sourceValue
                );
                
                // Create the target list type
                var listType = typeof(List<>).MakeGenericType(targetElementType);
                var constructor = listType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(targetElementType) });
                
                result = Expression.New(constructor, callMapCollection);
            }
            // Complex object conversion
            else if (!sourceType.IsPrimitive && !targetType.IsPrimitive && 
                     sourceType != typeof(string) && targetType != typeof(string))
            {
                // Use Map method to convert complex objects
                var mapMethod = typeof(Mapper).GetMethod("Map").MakeGenericMethod(sourceType, targetType);
                
                result = Expression.Call(
                    Expression.Constant(this),
                    mapMethod,
                    sourceValue
                );
            }
            // Simple type conversion
            else
            {
                try
                {
                    result = Expression.Convert(sourceValue, targetType);
                }
                catch
                {
                    // Fallback to default value if conversion is not possible
                    result = Expression.Default(targetType);
                }
            }
            
            // Wrap with null check
            return Expression.Condition(
                sourceNotNull,
                result,
                Expression.Default(targetType)
            );
        }
        else
        {
            // Value types can't be null (unless Nullable<T>)
            try
            {
                return Expression.Convert(sourceValue, targetType);
            }
            catch
            {
                // Fallback to default value if conversion is not possible
                return Expression.Default(targetType);
            }
        }
    }

    /// <summary>
    /// Checks if a type is a generic list or collection.
    /// </summary>
    private bool IsGenericList(Type type)
    {
        if (type == null)
            return false;
            
        return (type.IsGenericType && (
            type.GetGenericTypeDefinition() == typeof(List<>) ||
            type.GetGenericTypeDefinition() == typeof(IList<>) ||
            type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
            type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        ));
    }

    /// <summary>
    /// Gets a custom property mapping if configured.
    /// </summary>
    private string GetCustomPropertyMapping(string sourceName, string targetName, string targetPropertyName)
    {
        var key = $"{sourceName}=>{targetName}";
        if (_propertyMappings.TryGetValue(key, out var mappings) && 
            mappings.TryGetValue(targetPropertyName, out var sourcePropertyName))
        {
            return sourcePropertyName;
        }
        return null;
    }    /// <summary>
    /// Sets up a custom property mapping between types.
    /// </summary>
    internal void SetPropertyMapping(string sourceName, string targetName, string sourceProperty, string targetProperty)
    {
        var key = $"{sourceName}=>{targetName}";
        if (!_propertyMappings.TryGetValue(key, out var mappings))
        {
            mappings = new Dictionary<string, string>();
            _propertyMappings[key] = mappings;
        }
        
        mappings[targetProperty] = sourceProperty;
    }
    
    /// <summary>
    /// Marks a property to be ignored during mapping.
    /// </summary>
    internal void IgnoreProperty(string targetTypeName, string targetPropertyName)
    {
        if (!_ignoredProperties.TryGetValue(targetTypeName, out var ignored))
        {
            ignored = new HashSet<string>();
            _ignoredProperties[targetTypeName] = ignored;
        }
        
        ignored.Add(targetPropertyName);
        
        // Clear cache to ensure updated mappings are used
        _mapCache.Clear();
    }
    
    /// <summary>
    /// Checks if a property should be ignored during mapping.
    /// </summary>
    private bool IsPropertyIgnored(string targetTypeName, string targetPropertyName)
    {
        return _ignoredProperties.TryGetValue(targetTypeName, out var ignored) && 
               ignored.Contains(targetPropertyName);
    }
}

/// <summary>
/// Builder class for configuring mappings between types.
/// </summary>
public class MappingConfiguration<TSource, TTarget> where TTarget : new()
{
    private readonly Mapper _mapper;
    private readonly string _sourceName;
    private readonly string _targetName;

    public MappingConfiguration(Mapper mapper)
    {
        _mapper = mapper;
        _sourceName = typeof(TSource).Name;
        _targetName = typeof(TTarget).Name;
    }    /// <summary>
    /// Maps a property from the source to a different property name in the target.
    /// </summary>
    /// <param name="sourceProperty">Expression to select the source property</param>
    /// <param name="targetProperty">Expression to select the target property</param>
    /// <returns>The mapping configuration for further configuration</returns>
    public MappingConfiguration<TSource, TTarget> ForMember<TSourceProp, TTargetProp>(
        Expression<Func<TSource, TSourceProp>> sourceProperty,
        Expression<Func<TTarget, TTargetProp>> targetProperty)
    {
        var sourcePropInfo = GetPropertyInfo(sourceProperty);
        var targetPropInfo = GetPropertyInfo(targetProperty);

        _mapper.SetPropertyMapping(_sourceName, _targetName, sourcePropInfo.Name, targetPropInfo.Name);
        return this;
    }
    
    /// <summary>
    /// Ignores a property during mapping.
    /// </summary>
    /// <param name="targetProperty">Expression to select the target property to ignore</param>
    /// <returns>The mapping configuration for further configuration</returns>
    public MappingConfiguration<TSource, TTarget> Ignore<TProperty>(
        Expression<Func<TTarget, TProperty>> targetProperty)
    {
        var targetPropInfo = GetPropertyInfo(targetProperty);
        _mapper.IgnoreProperty(_targetName, targetPropInfo.Name);
        return this;
    }

    /// <summary>
    /// Extracts PropertyInfo from an expression.
    /// </summary>
    private PropertyInfo GetPropertyInfo<T, TProperty>(Expression<Func<T, TProperty>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return (PropertyInfo)memberExpression.Member;
        }
        throw new ArgumentException("Expression is not a valid property expression.", nameof(expression));
    }
}
