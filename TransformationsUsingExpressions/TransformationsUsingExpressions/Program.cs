using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace TransformationsUsingExpressions
{
    class Program
    {

        static void Main(string[] args)
        {
            //get source data
            dynamic[] sourceData = new dynamic[] {
            new {Name = "John", Age = 53, fk = 1},
            new {Name = "Alex", Age = 22, fk = 2},
            new {Name = "Angela", Age = 33, fk = 3},
            new {Name = "Angela", Age = 33, fk = 4}
            };
            //create example object for type resolution in generic classes
            dynamic example = sourceData[0];
            //Compile transformation expression
            dynamic transformer = Transformer(example);
            //transform data
            var transformed = sourceData.Select(x => transformer(x));

            //print results
            var enumerator = transformed.GetEnumerator();
            foreach(var source in sourceData)
            {
                enumerator.MoveNext();
                Console.WriteLine($"{source.Name} => {enumerator.Current.Name}");
                Console.WriteLine($"{source.Age} => {enumerator.Current.Name}");
                Console.WriteLine($"{source.fk} => {enumerator.Current.fk}");
                Console.WriteLine($"~~~~~~~~~~~~~~~~~~~~~");
            }
        }
        

        /// <summary>
        /// Generates a delegate that accepts a source object 
        /// and returns a clone with foreign key mappings applied
        /// </summary>
        public static Func<T, T> Transformer<T>(T example)
        {
            var type = typeof(T);
            var parentParameter = Expression.Parameter(typeof(T));
            //Get first constructor
            var ctor = type.GetConstructors()[0];
            //Get parameters for contstructor
            var ctorParams = ctor.GetParameters();
            //Construct expressions to resolve each parameter from the source object and apply foreign key mapping if applicable
            var parameterExpressions = ctorParams.Select(parameter => ApplyForeignKeyMap(example, parameter, parentParameter));
            //Build expression to execute the constructor 
            var constructorExpression = Expression.New(ctor, parameterExpressions);
            //Compile expression to a func<T,T>
            return Expression.Lambda<Func<T,T>>(constructorExpression, parentParameter).Compile();
        }

        /// <summary>
        /// Returns an expression that reads a parameter from the source object and if applicable, applies the foreign key map
        /// </summary>
        private static InvocationExpression ApplyForeignKeyMap<T>(T example, ParameterInfo parameter, ParameterExpression parentParameter)
        {
            return ForeignKeyTransformer.IsForeignKey(parameter.Name)
                //Convert to invocation expression (I am calling map foreign key)
                ? Expression.Invoke(
                    //Build expression to call MapForeignKey method with argument parameter 
                    ExpressionHelpers.GenerateForeignKeyTransformation(
                        //Build expression to read parameter from source object
                        ExpressionHelpers.GetProperty(example, (dynamic)typeof(T).GetProperty(parameter.Name).GetValue(example), parameter.Name), parameter.Name),
                    parentParameter)
                //Convert to invocation expression (I am calling the parameters get method)
                : Expression.Invoke(
                    //Build expression to read parameter from source object
                    ExpressionHelpers.GetProperty(example, (dynamic)typeof(T).GetProperty(parameter.Name).GetValue(example), parameter.Name),
                    parentParameter);
        }
    }

    public static class ForeignKeyTransformer
    {
        public static string[] foreignKeyColumns = new string[] { "fk" };
        public static Dictionary<string, Dictionary<dynamic, dynamic>> ForeignKeyMap;

        static ForeignKeyTransformer()
        {
            ForeignKeyMap = PopulateForeginKeyMap();
        }

        public static T TransformForeignKey<T>(T key, string propertyName)
        {
            return ForeignKeyMap[propertyName][key]; 
        }

        public static bool IsForeignKey(string propertyName)
        {
            return foreignKeyColumns.Contains(propertyName);
        }

        private static Dictionary<string, Dictionary<dynamic, dynamic>> PopulateForeginKeyMap()
        {
            var map = new Dictionary<string, Dictionary<dynamic, dynamic>>();
            map.Add("fk", new Dictionary<dynamic, dynamic>());
            map["fk"].Add(1, 12);
            map["fk"].Add(2, 13);
            map["fk"].Add(3, 14);
            map["fk"].Add(4, 15);
            return map;
        }
    }


    public static class ExpressionHelpers
    {
        /// <summary>
        /// Returns an expression that will read a property with a given name from a source object
        /// </summary>
        /// <typeparam name="T">Type of the source object</typeparam>
        /// <typeparam name="T2">Type of the property to be read</typeparam>
        /// <param name="sourceExample">Example object used to resolve Generic type T</param>
        /// <param name="propertyExample">Example object used to resolve Generic type T2</param>
        /// <param name="propertyName">Name of the property to be read</param>
        /// <returns></returns>
        public static Expression<Func<T, T2>> GetProperty<T, T2>(T sourceExample, T2 propertyExample, string propertyName)
        {
            var p = Expression.Parameter(typeof(T));
            var propertyReaderExp = Expression.PropertyOrField(p, propertyName);

            return Expression.Lambda<Func<T, T2>>(propertyReaderExp, p);
        }

        /// <summary>
        /// Invokes method <see cref="ForeignKeyTransformer.TransformForeignKey"/> on a property with a given name, that is resovled with a given expression
        /// </summary>
        /// <typeparam name="T">Type of the source object</typeparam>
        /// <typeparam name="T2">Type of the property to be transformed</typeparam>
        /// <param name="expr">Expression to resolve the property to be transformed</param>
        /// <param name="propertyName">Name of the property to be transformed</param>
        /// <returns></returns>
        public static Expression<Func<T, T2>> GenerateForeignKeyTransformation<T, T2>(Expression<Func<T, T2>> expr, string propertyName)
        {
            var propertyNameExpression = Expression.Constant(propertyName);
            var propertyValueExpressionParameter = Expression.Parameter(typeof(T));

            var invokedExpression = Expression.Invoke(expr, propertyValueExpressionParameter);

            var methodCallingExpression = Expression.Call(typeof(ForeignKeyTransformer).GetMethod(nameof(ForeignKeyTransformer.TransformForeignKey)).MakeGenericMethod(typeof(T2)), invokedExpression, propertyNameExpression);
            return Expression.Lambda<Func<T, T2>>(methodCallingExpression, propertyValueExpressionParameter);
        }
    }
}