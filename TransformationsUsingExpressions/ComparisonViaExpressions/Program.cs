using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ComparisonViaExpressions
{
    class Program
    {
        static void Main(string[] args)
        {

            dynamic[] left = new dynamic[] {
            new {Name = "John", Age = 53, fk = 12},
            new {Name = "Alex", Age = 22, fk = 13},
            new {Name = "Angela", Age = 33, fk = 12},
            new {Name = "George", Age = 23, fk = 15}
            };

                    dynamic[] right = new dynamic[] {
            new {Name = "John", Age = 53, fk = 1},
            new {Name = "Alex", Age = 21, fk = 2},
            new {Name = "Angela", Age = 33, fk = 3},
            new {Name = "george", Age = 23, fk = 4}
            };

            //Used for type identification in generic method
            var dummySourceInstance = left[0];
            var dynamicEqualityComparer = ExpressionHelpers.True(dummySourceInstance);
            foreach (var property in dummySourceInstance.GetType().GetProperties())
            {
                //Used for type identification in generic method
                var dummyPropertyInstance = property.GetValue(dummySourceInstance);

                //expression to read property from object
                var propertyExpression = ExpressionHelpers.GetPropertyExpression(dummySourceInstance, dummyPropertyInstance, property.Name);

                //expression to transform right property if foreign key
                var transformedPropertyExpression = ExpressionHelpers.GetTransformation(propertyExpression, property.Name);

                //build expression equivilant to propertyExpression(parameter1) == propertyExpression(parameter2)
                var equalityExpression = ExpressionHelpers.AreEqual(propertyExpression, transformedPropertyExpression);

                //append expression to existing expressiont tree with and operator
                dynamicEqualityComparer = ExpressionHelpers.And(dynamicEqualityComparer, equalityExpression);
            }

            //Compile expression tree
            var compiledExpression = dynamicEqualityComparer.Compile();

            //Execute expression over data set 
            Console.WriteLine("Key Mappings for fk");
            Console.WriteLine(string.Join(Environment.NewLine, ForeignKeyTransformer.ForeignKeyMap["fk"].Select(keyMap => $"{keyMap.Key} => {keyMap.Value}")));

            Console.WriteLine($"{Environment.NewLine}left.Name == right.Name && left.Age == right.Age && left.fk == right.fk == ??");

            for (int i = 0; i < left.Length; i++)
            {
                Console.Write($"{left[i].Name} == {right[i].Name} && {left[i].Age} == {right[i].Age} && {left[i].fk} == {right[i].fk} == ");
                Console.WriteLine(compiledExpression(left[i], right[i]));
            }
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
        public static Expression<Func<T, T, bool>> True<T>(T source) => (l, r) => true;

        public static Expression<Func<T, T, bool>> AreEqual<T, T2>(Expression<Func<T, T2>> left, Expression<Func<T, T2>> right)
        {
            var leftParam = Expression.Parameter(typeof(T));
            var rightParam = Expression.Parameter(typeof(T));

            var leftInvoked = Expression.Invoke(left, leftParam);
            var rightInvoked = Expression.Invoke(right, rightParam);

            return Expression.Lambda<Func<T, T, bool>>(Expression.Equal(leftInvoked, rightInvoked), new ParameterExpression[] { leftParam, rightParam });
        }

        public static Expression<Func<T, T2>> GetPropertyExpression<T, T2>(T source, T2 property, string propertyName)
        {
            var p = Expression.Parameter(typeof(T));
            var propertyReaderExp = Expression.PropertyOrField(p, propertyName);

            return Expression.Lambda<Func<T, T2>>(propertyReaderExp, p);
        }

        public static Expression<Func<T, T2>> GetTransformation<T, T2>(Expression<Func<T, T2>> expr, string propertyName)
        {
            if (ForeignKeyTransformer.IsForeignKey(propertyName))
            {
                var propertyNameExpression = Expression.Constant(propertyName);
                var propertyValueExpressionParameter = Expression.Parameter(typeof(T));

                var invokedExpression = Expression.Invoke(expr, propertyValueExpressionParameter);

                var methodCallingExpression = Expression.Call(typeof(ForeignKeyTransformer).GetMethod(nameof(ForeignKeyTransformer.TransformForeignKey)).MakeGenericMethod(typeof(T2)), invokedExpression, propertyNameExpression);
                return Expression.Lambda<Func<T, T2>>(methodCallingExpression, propertyValueExpressionParameter);
            }
            return expr;
        }

        public static Expression<Func<T, T, bool>> And<T>(Expression<Func<T, T, bool>> expr1,
                                                         Expression<Func<T, T, bool>> expr2)
        {
            var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>());
            return Expression.Lambda<Func<T, T, bool>>
                  (Expression.AndAlso(expr1.Body, invokedExpr), expr1.Parameters);
        }
    }
}
