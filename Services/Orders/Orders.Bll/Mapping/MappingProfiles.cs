using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Domain.Models;

namespace Orders.Bll.Mapping;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<Order, OrderDto>()
            .ForCtorParam(nameof(OrderDto.RowVersion), o => o.MapFrom(s => Convert.ToBase64String(s.RowVersion)));
        CreateMap<OrderSummary, OrderSummaryDto>();
        CreateMap<OrderStatusHistoryEntry, OrderStatusHistoryDto>();
    }
}

public class CustomerProfile : Profile
{
    public CustomerProfile() => CreateMap<Customer, CustomerDto>();
}

public class ProductProfile : Profile
{
    public ProductProfile() => CreateMap<Product, ProductDto>();
}
