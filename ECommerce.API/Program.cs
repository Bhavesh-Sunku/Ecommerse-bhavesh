using System.Text;

using ECommerce.API.Hubs;
using ECommerce.API.Middleware;
using ECommerce.API.Services;

using ECommerce.Business.BackgroundServices;
using ECommerce.Business.Interfaces;
using ECommerce.Business.Services;

using ECommerce.DataAccess.Context;
using ECommerce.DataAccess.Repositories.Implementations;
using ECommerce.DataAccess.Repositories.Interfaces;

using ECommerce.Domain.Entities;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
// Enable detailed errors and developer exception page in Development
// This helps show the real exception and stack trace instead of a generic 500.
builder.Services.Configure<Microsoft.AspNetCore.Builder.DeveloperExceptionPageOptions>(options =>
{
    // Keep default options; configure if needed
});

// ==========================================
// Controllers
// ==========================================

builder.Services.AddControllers();

// ==========================================
// SignalR
// ==========================================

builder.Services.AddSignalR();

// ==========================================
// Memory Cache
// ==========================================

builder.Services.AddMemoryCache();

// ==========================================
// JWT Authentication
// ==========================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is not configured. Set it with user secrets or an environment variable.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 UTF-8 bytes.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,

            ValidAudience = jwtAudience,

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// ==========================================
// Authorization
// ==========================================

builder.Services.AddAuthorization();

// ==========================================
// Swagger + JWT Bearer
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "ECommerce.API",
            Version = "v1"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "Enter your JWT token."
        });

    options.AddSecurityRequirement(
        document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "Bearer",
                document)] = []
        });
});

// ==========================================
// Entity Framework Core
// ==========================================

builder.Services.AddDbContext<EcommerceDbContext>(
    options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString(
                "DefaultConnection")));

// ==========================================
// Repositories
// ==========================================

builder.Services.AddScoped<
    IUserRepository,
    UserRepository>();

builder.Services.AddScoped<
    IProductRepository,
    ProductRepository>();

builder.Services.AddScoped<
    IInventoryRepository,
    InventoryRepository>();

builder.Services.AddScoped<
    IOrderRepository,
    OrderRepository>();

builder.Services.AddScoped<
    ICartRepository,
    CartRepository>();

builder.Services.AddScoped<
    IPaymentRepository,
    PaymentRepository>();

// ==========================================
// Business Services
// ==========================================

builder.Services.AddScoped<
    IProductService,
    ProductService>();

builder.Services.AddScoped<
    IInventoryService,
    InventoryService>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<
    IOrderService,
    OrderService>();

builder.Services.AddScoped<
    ICartService,
    CartService>();

builder.Services.AddScoped<
    IPaymentService,
    PaymentService>();

// ==========================================
// Order Notification Service
// ==========================================

builder.Services.AddScoped<
    IOrderNotificationService,
    OrderNotificationService>();

// ==========================================
// Password Hasher
// ==========================================

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

// ==========================================
// Background Services
// ==========================================

builder.Services.AddHostedService<
    OrderExpirationService>();

// ==========================================
// Build Application
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactClient", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var app = builder.Build();
// Show detailed exception page in Development environment
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}



// ==========================================
// Global Exception Handling
// ==========================================

app.UseMiddleware<ExceptionHandlingMiddleware>();

// ==========================================
// Swagger
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ==========================================
// Authentication
// ==========================================
app.UseCors("ReactClient");



app.UseAuthentication();

// ==========================================
// Authorization
// ==========================================

app.UseAuthorization();

// ==========================================
// Controllers
// ==========================================

app.MapControllers();

// ==========================================
// SignalR Hubs
// ==========================================

app.MapHub<OrderNotificationHub>(
    "/hubs/orders");

app.MapHub<ProductHub>(
    "/hubs/products");

// ==========================================
// Run Application
// ==========================================

app.Run();