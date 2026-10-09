using AutoMapper;
using Orders.Dal;
using Orders.Domain;

namespace Orders.Bll;

public interface ICustomerService
{
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto);
    Task<CustomerDto> GetByIdAsync(Guid id);
}

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CustomerService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessValidationException("Ім'я клієнта не може бути порожнім.");

        var customer = _mapper.Map<Customer>(dto);

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Customers.AddAsync(customer);
            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }

        return _mapper.Map<CustomerDto>(customer);
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id)
    {
        var customer = await _uow.Customers.GetByIdAsync(id);
        if (customer == null)
            throw new NotFoundException($"Покупця з ID '{id}' не знайдено.");

        return _mapper.Map<CustomerDto>(customer);
    }
}