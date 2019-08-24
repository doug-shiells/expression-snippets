using System;
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
            new {Name = "Alex", Age = 22, fk = 12},
            new {Name = "Angela", Age = 33, fk = 12}
            };

                    dynamic[] right = new dynamic[] {
            new {Name = "John", Age = 53, fk = 1},
            new {Name = "Alex", Age = 21, fk = 12},
            new {Name = "Angela", Age = 33, fk = 12}
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
            for (int i = 0; i < left.Length; i++)
            {
                Console.WriteLine(compiledExpression(left[i], right[i]));
            }
        }

    }
    public static class ForeignKeyTransformer
    {
        public static string[] foreignKeys = new string[] { "fk" };
        public static int TransformForeignKey(int key, string propertyName)
        {
            if (foreignKeys.Contains(propertyName))
            {
                Console.WriteLine("Mapping key for column " + propertyName);
                return 12; //Do actual mapping transformation here
            }
            else
                return key;
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
            if (typeof(T2) == typeof(int))
            {
                var propertyNameExpression = Expression.Constant(propertyName);
                var propertyValueExpressionParameter = Expression.Parameter(typeof(T));

                var invokedExpression = Expression.Invoke(expr, propertyValueExpressionParameter);

                var methodCallingExpression = Expression.Call(typeof(ForeignKeyTransformer).GetMethod("TransformForeignKey"), invokedExpression, propertyNameExpression);
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
