using Orders.Api;
using Orders.Bll;
using Orders.Dal;

var builder = WebApplication.CreateBuilder(args);

// 1. Отримуємо Connection String
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 2. Реєстрація залежностей (Dependency Injection)
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddScoped<IOrderService, OrderService>();

// AutoMapper
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile));

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 3. Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
await DbInitializer.InitializeAsync(connectionString);

app.Run();
