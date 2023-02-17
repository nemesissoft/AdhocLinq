using System.Linq.Expressions;
using AdhocLinq;


ParseDates();



void ParseDates()
{
    var dynExp = DynamicExpressionFactory.DefaultFactory.Create();

    var parsed = dynExp.Parse(typeof(string), """
DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + " DESC " + (DateTime.Today + TimeSpan.FromHours(1)).ToString("HHmmss.ffff", CultureInfo.InvariantCulture)
"""
    );

    var lambda = Expression.Lambda<Func<string>>(parsed);
    var getter = lambda.Compile();

    Console.WriteLine(getter());
}
