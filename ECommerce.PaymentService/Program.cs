using System.Text;
using ECommerce.Payments.Context;
using ECommerce.Payments.Services;
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
        Title = "ECommerce Payment Service (Mock Provider)",
        Version = "v1"
    });
});

var connectionString =
    builder.Configuration.GetConnectionString("PaymentService")
    ?? "Server=(localdb)\\mssqllocaldb;Database=ECommercePayments;Trusted_Connection=True;TrustServerCertificate=True;";
builder.Services.AddDbContext<PaymentDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHttpClient<IOrderClient, OrderClient>(client =>
{
    var orderServiceUrl = builder.Configuration["OrderService:BaseUrl"]
        ?? "http://localhost:5203/";
    client.BaseAddress = new Uri(orderServiceUrl.EndsWith('/')
        ? orderServiceUrl
        : $"{orderServiceUrl}/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<IMockPaymentProvider, MockPaymentProvider>();
builder.Services.AddScoped<PaymentManagementService>();

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
    var (statusCode, title) = exception switch
    {
        InvalidOperationException => (StatusCodes.Status409Conflict, "Payment request cannot be completed."),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "Order was not found."),
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Order access was denied."),
        OrderServiceException { StatusCode: StatusCodes.Status409Conflict } =>
            (StatusCodes.Status409Conflict, "Order cannot be used for this payment attempt."),
        OrderServiceException => (StatusCodes.Status502BadGateway, "Order Service request failed."),
        HttpRequestException => (StatusCodes.Status502BadGateway, "Order Service is unavailable."),
        TaskCanceledException when !context.RequestAborted.IsCancellationRequested =>
            (StatusCodes.Status504GatewayTimeout, "Order Service timed out."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };
    logger.Log(
        statusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
        exception,
        "Payment request failed for {Path} with status {StatusCode}",
        context.Request.Path,
        statusCode);
    context.Response.StatusCode = statusCode;
    await Results.Problem(title: title, statusCode: statusCode).ExecuteAsync(context);
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
