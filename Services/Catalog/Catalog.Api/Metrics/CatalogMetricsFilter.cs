using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Catalog.Api.Metrics;

/// <summary>Action filter: тривалість усіх дій (успішних і невдалих) та бізнес-лічильники після успіху.</summary>
public sealed class CatalogMetricsFilter(CatalogMetrics metrics) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var route = context.ActionDescriptor.RouteValues;
        var controller = route.GetValueOrDefault("controller") ?? "unknown";
        var operation = $"{controller}.{route.GetValueOrDefault("action") ?? "unknown"}";

        metrics.OperationStarted();
        var sw = Stopwatch.StartNew();
        ActionExecutedContext? executed = null;
        try
        {
            executed = await next();
        }
        finally
        {
            var failed = executed is null
                         || (executed.Exception is not null && !executed.ExceptionHandled)
                         || executed.Result is ObjectResult { StatusCode: >= 400 }
                         || executed.Result is StatusCodeResult { StatusCode: >= 400 };
            metrics.OperationFinished(operation, success: !failed, sw.Elapsed.TotalMilliseconds);

            if (!failed && controller == "Products") CountBusinessEvent(operation);
        }
    }

    private void CountBusinessEvent(string operation)
    {
        switch (operation)
        {
            case "Products.Create": metrics.ProductCreated(); break;
            case "Products.Update": metrics.ProductUpdated("details"); break;
            case "Products.ChangePrice": metrics.ProductUpdated("price"); break;
            case "Products.SaveDetail": metrics.ProductUpdated("detail"); break;
            case "Products.AddImage": metrics.ProductUpdated("image"); break;
            case "Products.AddStock": metrics.StockChanged("add"); break;
            case "Products.RemoveStock": metrics.StockChanged("remove"); break;
            case "Products.AssignCategory":
            case "Products.RemoveCategory": metrics.CategoriesChanged(); break;
            case "Products.Delete": metrics.ProductDeleted(); break;
        }
    }
}
