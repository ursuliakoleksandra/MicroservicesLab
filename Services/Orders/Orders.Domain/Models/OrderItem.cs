namespace Orders.Domain.Models;

public class OrderItem
{
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";   // ЗНІМОК на момент замовлення (політика дублювання, Лаб. 01)
    public decimal UnitPrice { get; set; }          // ЗНІМОК
    public decimal Discount { get; set; }           // сума знижки на весь рядок
    public int Units { get; set; }

    public decimal LineTotal => UnitPrice * Units - Discount;
}
