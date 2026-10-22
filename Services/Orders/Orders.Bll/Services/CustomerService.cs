using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Dal.Interfaces;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

public sealed class CustomerService(IUnitOfWork uow, IMapper mapper, ILogger<CustomerService> logger) : ICustomerService
{
    private const string Actor = "api";

    public async Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct) =>
        mapper.Map<CustomerDto>(await uow.Customers.GetByIdAsync(id, ct)
                                ?? throw new NotFoundException($"Customer {id} not found"));

    public async Task<PagedResultDto<CustomerDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) throw new ValidationException("Page must be >= 1");
        if (pageSize is < 1 or > 100) throw new ValidationException("PageSize must be between 1 and 100");

        var rows = await uow.Customers.GetPagedAsync(page, pageSize, ct);
        return new PagedResultDto<CustomerDto>(mapper.Map<List<CustomerDto>>(rows), rows.Count, page, pageSize);
    }

    public async Task<CustomerDto> CreateAsync(SaveCustomerDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim();
        if (await uow.Customers.GetByEmailAsync(email, ct) is not null)
            throw new BusinessConflictException("Customer with the same email already exists");

        var id = await uow.Customers.AddAsync(
            new Customer { Email = email, FullName = dto.FullName.Trim(), Phone = dto.Phone }, Actor, ct);
        logger.LogInformation("Customer {CustomerId} created", id);   // email у лог не пишемо (персональні дані)
        return await GetByIdAsync(id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(long id, SaveCustomerDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim();
        var sameEmail = await uow.Customers.GetByEmailAsync(email, ct);
        if (sameEmail is not null && sameEmail.Id != id)
            throw new BusinessConflictException("Customer with the same email already exists");

        var updated = await uow.Customers.UpdateAsync(
            new Customer { Id = id, Email = email, FullName = dto.FullName.Trim(), Phone = dto.Phone }, Actor, ct);
        if (!updated) throw new NotFoundException($"Customer {id} not found");
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        if (!await uow.Customers.SoftDeleteAsync(id, Actor, ct))
            throw new NotFoundException($"Customer {id} not found");
        logger.LogInformation("Customer {CustomerId} soft-deleted", id);
    }
}
