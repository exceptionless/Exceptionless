namespace Exceptionless.Web.Api.Infrastructure;

public static class Pagination
{
    public const int DefaultLimit = 10;
    public const int MaximumLimit = 100;
    public const int MaximumSkip = 1000;

    public static int GetLimit(int limit, int maximumLimit = MaximumLimit)
    {
        if (limit < 1)
            limit = DefaultLimit;
        else if (limit > maximumLimit)
            limit = maximumLimit;

        return limit;
    }

    public static int GetPage(int page)
    {
        if (page < 1)
            page = 1;

        return page;
    }

    public static int GetSkip(int currentPage, int limit)
    {
        if (currentPage < 1)
            currentPage = 1;

        int skip = (currentPage - 1) * limit;
        if (skip < 0)
            skip = 0;

        return skip;
    }

    public static bool NextPageExceedsSkipLimit(int? page, int limit)
    {
        if (page is null)
            return false;

        return (page + 1) * limit >= MaximumSkip;
    }
}
