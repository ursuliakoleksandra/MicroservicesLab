using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

public record CustomerDto(long Id, string Email, string FullName, string? Phone, DateTime CreatedAt);

public class SaveCustomerDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [StringLength(30)]
    public string? Phone { get; set; }
}

public record ProductDto(long Id, string Name, decimal Price, bool IsActive, int Stock);

/// <summary>Синхронізація локальної копії довідника з Каталогу (імітація події, Лаб. 08).</summary>
public class SyncProductDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Початковий залишок — використовується лише при створенні нового запису.</summary>
    [Range(0, 1_000_000)]
    public int Stock { get; set; }
}
