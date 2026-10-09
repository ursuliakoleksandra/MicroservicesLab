using System.ComponentModel.DataAnnotations;
using Orders.Domain;

namespace Orders.Bll;

public class CreateOrderItemDto
{
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Кількість має бути більшою за 0.")]
    public int Quantity { get; set; }
}

public class CreateOrderDto
{
    public Guid CustomerId { get; set; }
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class CreateCustomerDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;
}

public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}