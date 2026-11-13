using System.Text;
using ECommerce.OrderService.Context;
using ECommerce.OrderService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ECommerce Order Service",
        Version = "v1"
    });
});

var connectionString =
    builder.Configuration.GetConnectionString("OrderService")
    ?? "Server=(localdb)\\mssqllocaldb;Database=ECommerceOrders;Trusted_Connection=True;TrustServerCertificate=True;";
builder.Services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHttpClient<ProductCatalogClient>(client =>
{
    var productServiceUrl = builder.Configuration["ProductService:BaseUrl"]
        ?? "http://localhost:5201/";
    client.BaseAddress = new Uri(productServiceUrl.EndsWith('/')
        ? productServiceUrl
        : $"{productServiceUrl}/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<InventoryReservationClient>(client =>
{
    var productServiceUrl = builder.Configuration["ProductService:BaseUrl"]
        ?? "http://localhost:5201/";
    client.BaseAddress = new Uri(productServiceUrl.EndsWith('/')
        ? productServiceUrl
        : $"{productServiceUrl}/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<OrderManagementService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("JWT key must contain at least 32 UTF-8 bytes.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features
        .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    if (exception is InvalidOperationException)
    {
        logger.LogWarning(exception, "Rejected order-service request for {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await Results.Problem(
            title: "Invalid request",
            detail: exception.Message,
            statusCode: context.Response.StatusCode).ExecuteAsync(context);
        return;
    }

    if (exception is HttpRequestException)
    {
        logger.LogError(exception, "Product service is unavailable for {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await Results.Problem(
            title: "Product service is unavailable.",
            statusCode: context.Response.StatusCode).ExecuteAsync(context);
        return;
    }

    if (exception is TaskCanceledException &&
        !context.RequestAborted.IsCancellationRequested)
    {
        logger.LogError(exception, "Product service timed out for {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        await Results.Problem(
            title: "Product service timed out.",
            statusCode: context.Response.StatusCode).ExecuteAsync(context);
        return;
    }

    logger.LogError(exception, "Unhandled order-service error for {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await Results.Problem(
        title: "An unexpected error occurred.",
        statusCode: context.Response.StatusCode).ExecuteAsync(context);
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();
app.Run();
