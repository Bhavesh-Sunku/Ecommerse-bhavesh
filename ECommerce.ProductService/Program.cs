using System.Text;
using ECommerce.ProductService.Context;
using ECommerce.ProductService.Repositories;
using ECommerce.ProductService.Services;
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
        Title = "ECommerce Product Service",
        Version = "v1"
    });
});

var connectionString =
    builder.Configuration.GetConnectionString("ProductService")
    ?? "Server=(localdb)\\mssqllocaldb;Database=ECommerceProductsDb;Trusted_Connection=True;TrustServerCertificate=True;";
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddMemoryCache();
var internalServiceKey = builder.Configuration["InternalServices:ApiKey"]
    ?? throw new InvalidOperationException("Internal service API key is not configured.");
if (Encoding.UTF8.GetByteCount(internalServiceKey) < 32)
{
    throw new InvalidOperationException("Internal service API key must contain at least 32 UTF-8 bytes.");
}

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

builder.Services.AddScoped<ProductCatalogService>();
builder.Services.AddScoped<InventoryManagementService>();
builder.Services.AddScoped<InventoryReservationService>();
builder.Services.AddSingleton<InternalServiceKeyValidator>();
builder.Services.AddScoped<IProductRepository, ProductServiceProductRepository>();
builder.Services.AddScoped<IInventoryRepository, ProductServiceInventoryRepository>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?
            .Error;
        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        if (exception is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(
                exception,
                "Rejected invalid product-service request for {Path}",
                context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.Problem(
                title: "Invalid request",
                detail: exception.Message,
                statusCode: context.Response.StatusCode)
                .ExecuteAsync(context);
            return;
        }

        logger.LogError(
            exception,
            "Unhandled product-service error for {Path}",
            context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            title: "An unexpected error occurred.",
            statusCode: context.Response.StatusCode)
            .ExecuteAsync(context);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();

app.Run();
