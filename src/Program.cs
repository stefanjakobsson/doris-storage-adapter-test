using DatasetFileUpload;
using DatasetFileUpload.Authorization;
using DatasetFileUpload.Controllers;
using DatasetFileUpload.Services;
using DatasetFileUpload.Services.Exceptions;
using DatasetFileUpload.Services.Lock;
using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using NetDevPack.Security.Jwt.Core.Interfaces;
using NetDevPack.Security.JwtExtensions;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptionsWithValidateOnStart<GeneralConfiguration>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<AuthorizationConfiguration>()
    .Bind(builder.Configuration.GetSection(AuthorizationConfiguration.ConfigurationSection))
    .ValidateDataAnnotations();

static void SetupJsonSerializer(JsonSerializerOptions options)
{
    options.Converters.Add(new JsonStringEnumConverter());
}

builder.Services.AddControllers().AddJsonOptions(options => SetupJsonSerializer(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => SetupJsonSerializer(options.SerializerOptions));

// Map ApiExceptions to problem details response
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        var exception = ctx.HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (exception != null && exception is ApiException apiException)
        {
            ctx.ProblemDetails.Detail = apiException.Message;
            ctx.ProblemDetails.Status = apiException.StatusCode;
            ctx.HttpContext.Response.StatusCode = apiException.StatusCode;
        }
    };
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "JWT Authentication",
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme
    }
    );
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddMemoryCache(); // Needed for AddJwksManager()
    builder.Services.AddJwksManager().PersistKeysInMemory();
}

var authorizationConfiguration = builder.Configuration
    .GetSection(AuthorizationConfiguration.ConfigurationSection)
    .Get<AuthorizationConfiguration>()!;

var generalConfiguration = builder.Configuration.Get<GeneralConfiguration>()!;

// Set up JWT authentication/authorization
builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.SetJwksOptions(
        new(
            jwksUri: authorizationConfiguration.JwksUri,
            audience: generalConfiguration.PublicUrl
        ));

    // Limiting the valid algorithms to only the one used by Doris hardens security by
    // making algorithm confusion attacks impossible, but also means that it's harder
    // for SND to change the signing algorithm.
    options.TokenValidationParameters.ValidAlgorithms = ["PS256"];
});

// Setup up CORS policys per endpoint
builder.Services.AddCors(options =>
{
    options.AddPolicy(nameof(FileController.StoreFile), policy =>
    {
        policy.WithOrigins(authorizationConfiguration.CorsAllowedOrigins);
        policy.WithHeaders(HeaderNames.ContentLength);
        policy.WithMethods(HttpMethods.Put);
    });
    options.AddPolicy(nameof(FileController.DeleteFile), policy =>
    {
        policy.WithOrigins(authorizationConfiguration.CorsAllowedOrigins);
        policy.WithMethods(HttpMethods.Delete);
    });
    options.AddPolicy(nameof(FileController.GetFileData), policy =>
    {
        policy.WithOrigins(authorizationConfiguration.CorsAllowedOrigins);
        policy.WithMethods(HttpMethods.Get);
        policy.WithExposedHeaders(HeaderNames.ContentDisposition);
    });
    options.AddPolicy(nameof(FileController.GetFileDataAsZip), policy =>
    {
        policy.WithOrigins(authorizationConfiguration.CorsAllowedOrigins);
        policy.WithMethods(HttpMethods.Get);
        policy.WithExposedHeaders(HeaderNames.ContentDisposition);
    });
});

builder.Services.AddSingleton<ILockService, InProcessLockService>();
builder.Services.AddTransient<ServiceImplementation>();

// Setup storage service based on configuration
void SetupStorageService()
{
    var configSection = builder.Configuration.GetSection("Storage");
    const string configKey = "ActiveStorageService";
    string storageService = configSection.GetValue<string>(configKey) ??
        throw new ApplicationException($"{configKey} not set.");

    var types = typeof(Program).Assembly.GetTypes()
        .Where(t =>
            t.GetInterfaces().Any(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IStorageServiceConfigurer<>) &&
                i.GenericTypeArguments[0].Name == storageService))
        .ToList();

    if (types.Count == 0)
    {
        throw new ApplicationException($"{storageService} not found.");
    }
    else if (types.Count > 1)
    {
        throw new ApplicationException($"Multiple implementations of {storageService} found.");
    }

    var configurer = Activator.CreateInstance(types[0]) as IStorageServiceConfigurerBase;
    configurer!.Configure(builder.Services, configSection.GetSection(storageService));
}

SetupStorageService();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseJwksDiscovery();

    using var scope = app.Services.CreateScope();
    var jwtService = scope.ServiceProvider.GetService<IJwtService>()!;
    var key = await jwtService.GetCurrentSigningCredentials();

    string CreateToken(params string[] roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = generalConfiguration.PublicUrl,
            Audience = generalConfiguration.PublicUrl,
            Subject = new(roles.Select(r => new Claim("role", r))),
            Expires = DateTime.UtcNow.AddHours(12),
            SigningCredentials = key
        });

        return tokenHandler.WriteToken(token);
    }

    Console.WriteLine("Service token:");
    Console.WriteLine(CreateToken(Roles.Service));
}

app.Run();
