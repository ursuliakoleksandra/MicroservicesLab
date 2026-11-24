using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Orders.Bll.Dtos;

namespace Orders.Api.Metrics;

/// <summary>
/// Action filter: вимірює тривалість КОЖНОЇ дії контролерів (успішної й невдалої) і збільшує бізнес-лічильники після успіху.
/// Бізнес-логіка сервісів не змінюється — метрики збираються на межі API.
/// </summary>
public sealed class OrderMetricsFilter(OrderMetrics metrics) : IAsyncActionFilter
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

            if (!failed && controller == "Orders") CountBusinessEvent(operation, context, executed!);
        }
    }

    private void CountBusinessEvent(string operation, ActionExecutingContext context, ActionExecutedContext executed)
    {
        switch (operation)
        {
            case "Orders.Create":
                metrics.OrderCreated((executed.Result as ObjectResult)?.Value is OrderDto dto ? dto.Status : "Submitted");
                break;
            case "Orders.Confirm": metrics.OrderConfirmed(); break;
            case "Orders.Pay":
                metrics.OrderPaid(context.ActionArguments.Values.OfType<PayOrderDto>().FirstOrDefault()?.PaymentMethod ?? "unknown");
                break;
            case "Orders.Ship": metrics.OrderShipped(); break;
            case "Orders.Cancel": metrics.OrderCancelled(); break;
            case "Orders.Delete": metrics.OrderDeleted(); break;
        }
    }
}
