using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddControllers();

        // Application services
        builder.Services.AddSingleton<IUserService, InMemoryUserService>();

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Missing '{JwtOptions.SectionName}' configuration section.");
        if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long.");
        }
        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddSingleton<ITokenService, JwtTokenService>();

        // Authentication: validate bearer JWTs issued by the login endpoint
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names as issued (e.g. "sub") instead of mapping them to long ClaimTypes URIs
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwtOptions.GetSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        // Authorization: endpoints opt in with [Authorize]
        builder.Services.AddAuthorization();

        var app = builder.Build();

        // Log every HTTP call; registered first so it also sees responses short-circuited by later middleware (e.g. 401s)
        app.Use(async (context, next) =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next(context);
                app.Logger.LogInformation(
                    "HTTP {Method} {Path}{QueryString} responded {StatusCode} in {ElapsedMs} ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // The status code is only set to 500 further out in the pipeline, so log it explicitly here
                app.Logger.LogError(ex,
                    "HTTP {Method} {Path}{QueryString} failed with 500 in {ElapsedMs} ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        });

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
