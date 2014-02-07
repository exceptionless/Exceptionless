using System.Web.Routing;

public class LowercaseRoute : System.Web.Routing.Route
{
    public LowercaseRoute(string url, IRouteHandler routeHandler)
        : base(url, routeHandler) { }
    public LowercaseRoute(string url, RouteValueDictionary defaults, IRouteHandler routeHandler)
        : base(url, defaults, routeHandler) { }
    public LowercaseRoute(string url, RouteValueDictionary defaults, RouteValueDictionary constraints, IRouteHandler routeHandler)
        : base(url, defaults, constraints, routeHandler) { }
    public LowercaseRoute(string url, RouteValueDictionary defaults, RouteValueDictionary constraints, RouteValueDictionary dataTokens, IRouteHandler routeHandler)
        : base(url, defaults, constraints, dataTokens, routeHandler) { }

    public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
    {
        VirtualPathData path = base.GetVirtualPath(requestContext, values);

        if (path != null)
            path.VirtualPath = path.VirtualPath.ToLowerInvariant();

        return path;
    }
}