using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace Exceptionless.Web.Utility;

public class DelimitedQueryStringValueProvider : QueryStringValueProvider
{
    private readonly CultureInfo _culture;
    private readonly char[] _delimiters;
    private readonly IQueryCollection _queryCollection;

    public DelimitedQueryStringValueProvider(BindingSource bindingSource, IQueryCollection values, CultureInfo culture, char[] delimiters) : base(bindingSource, values, culture)
    {
        _queryCollection = values;
        _culture = culture;
        _delimiters = delimiters;
    }

    public override ValueProviderResult GetValue(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var values = _queryCollection[key];
        if (values.Count == 0)
            return ValueProviderResult.None;

        if (!values.Any(x => x != null && _delimiters.Any(x.Contains)))
            return new ValueProviderResult(values, _culture);

        var stringValues = new StringValues(values.SelectMany(x => x?.Split(_delimiters, StringSplitOptions.RemoveEmptyEntries) ?? []).ToArray());
        return new ValueProviderResult(stringValues, _culture);
    }
}

public class DelimitedQueryStringValueProviderFactory : IValueProviderFactory
{
    private static readonly char[] DefaultDelimiters = [','];
    private readonly char[] _delimiters;

    public DelimitedQueryStringValueProviderFactory() : this(DefaultDelimiters) { }

    public DelimitedQueryStringValueProviderFactory(params char[] delimiters)
    {
        _delimiters = delimiters.Length == 0 ? DefaultDelimiters : delimiters;
    }

    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var valueProvider = new DelimitedQueryStringValueProvider(BindingSource.Query, context.ActionContext.HttpContext.Request.Query, CultureInfo.InvariantCulture, _delimiters);

        context.ValueProviders.Add(valueProvider);

        return Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class DelimitedQueryStringAttribute : Attribute, IResourceFilter
{
    private readonly char[] _delimiters;

    public DelimitedQueryStringAttribute(params char[] delimiters)
    {
        _delimiters = delimiters;
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ValueProviderFactories.AddDelimitedValueProviderFactory(_delimiters);
    }
}

public static class ValueProviderFactoriesExtensions
{
    public static void AddDelimitedValueProviderFactory(this IList<IValueProviderFactory> valueProviderFactories, char[] delimiters)
    {
        var queryStringValueProviderFactory = valueProviderFactories.OfType<QueryStringValueProviderFactory>().FirstOrDefault();

        if (queryStringValueProviderFactory is null)
        {
            valueProviderFactories.Insert(0, new DelimitedQueryStringValueProviderFactory(delimiters));
        }
        else
        {
            valueProviderFactories.Insert(valueProviderFactories.IndexOf(queryStringValueProviderFactory), new DelimitedQueryStringValueProviderFactory(delimiters));
            valueProviderFactories.Remove(queryStringValueProviderFactory);
        }
    }
}
