using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireRole("Admin"));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174",
            "http://127.0.0.1:5173",
            "http://127.0.0.1:5174")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilder =>
    {
        transformBuilder.AddRequestTransform(transformContext =>
        {
            var correlationId =
                transformContext.HttpContext.Items["CorrelationId"]?.ToString();
            if (correlationId is not null)
            {
                transformContext.ProxyRequest.Headers.Remove("X-Correlation-ID");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                    "X-Correlation-ID",
                    correlationId);
            }

            transformContext.ProxyRequest.Headers.Remove("X-User-Id");
            transformContext.ProxyRequest.Headers.Remove("X-User-Role");
            transformContext.ProxyRequest.Headers.Remove("X-Authenticated-User");
            transformContext.ProxyRequest.Headers.Remove("X-Authenticated-Role");
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features
        .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(exception, "Gateway error for {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status502BadGateway;
    await Results.Problem(
        title: "The requested service is unavailable.",
        statusCode: context.Response.StatusCode).ExecuteAsync(context);
}));

app.Use(async (context, next) =>
{
    var correlationId = Guid.NewGuid().ToString("N");
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    var stopwatch = Stopwatch.StartNew();

    try
    {
        await next();
    }
    finally
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms; correlation {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            correlationId);
    }
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapReverseProxy();
app.Run();
