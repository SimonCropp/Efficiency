namespace Delta;

public static partial class DeltaExtensions
{
    public static ComponentEndpointConventionBuilder UseDelta(this ComponentEndpointConventionBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<ComponentEndpointConventionBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static ConnectionEndpointRouteBuilder UseDelta(ConnectionEndpointRouteBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<ConnectionEndpointRouteBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static ControllerActionEndpointConventionBuilder UseDelta(this ControllerActionEndpointConventionBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<ControllerActionEndpointConventionBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static HubEndpointConventionBuilder UseDelta(this HubEndpointConventionBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<HubEndpointConventionBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static IHubEndpointConventionBuilder UseDelta(this IHubEndpointConventionBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<IHubEndpointConventionBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static PageActionEndpointConventionBuilder UseDelta(this PageActionEndpointConventionBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<PageActionEndpointConventionBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static RouteGroupBuilder UseDelta(this RouteGroupBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<RouteGroupBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static RouteHandlerBuilder UseDelta(this RouteHandlerBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug) =>
        builder.UseDelta<RouteHandlerBuilder>(getConnection, suffix, shouldExecute, logLevel);

    public static IApplicationBuilder UseDelta(this IApplicationBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug)
    {
        var loggerFactory = builder.ApplicationServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Delta");
        return builder.Use(
            async (context, next) =>
            {
                if (await HandleRequest(context, getConnection, logger, suffix, shouldExecute, logLevel))
                {
                    return;
                }

                await next();
            });
    }

    public static TBuilder UseDelta<TBuilder>(this TBuilder builder, GetConnection getConnection, Func<HttpContext, string?>? suffix = null, Func<HttpContext, bool>? shouldExecute = null, LogLevel logLevel = LogLevel.Debug)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilterFactory((filterContext, next) =>
        {
            var loggerFactory = filterContext.ApplicationServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Delta");
            return async invocationContext =>
            {
                if (await HandleRequest(invocationContext.HttpContext, getConnection, logger, suffix, shouldExecute, logLevel))
                {
                    return Results.Empty;
                }

                return await next(invocationContext);
            };
        });

    internal static Task<bool> HandleRequest(
        HttpContext context,
        GetConnection getConnection,
        ILogger logger,
        Func<HttpContext, string?>? suffix,
        Func<HttpContext, bool>? shouldExecute,
        LogLevel logLevel) =>
        HandleRequest(
            context,
            logger,
            suffix,
            _ =>
            {
                var (connection, transaction) = getConnection(_);
                return GetLastTimeStamp(connection, transaction);
            },
            shouldExecute,
            logLevel);
}