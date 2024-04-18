using DatasetFileUpload.Authorization;
using DatasetFileUpload.Configuration;
using DatasetFileUpload.Services;
using DatasetFileUpload.Services.Lock;
using DatasetFileUpload.Services.Storage;
using DatasetFileUpload.Services.Storage.Disk;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NetDevPack.Security.Jwt.Core.Interfaces;
using NetDevPack.Security.JwtExtensions;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptionsWithValidateOnStart<FileSystemStorageServiceConfiguration>()
    .Bind(builder.Configuration.GetSection(FileSystemStorageServiceConfiguration.ConfigurationSection))
    .ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<GeneralConfiguration>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations();

builder.Services.AddControllers().AddJsonOptions(options => 
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

var generalConfiguration = builder.Configuration.Get<GeneralConfiguration>()!;

builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.SetJwksOptions(
        new(
            jwksUri: generalConfiguration.JwksUri, 
            audience: generalConfiguration.PublicUrl
        ));

    // Limiting the valid algorithms to only the one used by Doris hardens security by
    // making algorithm confusion attacks impossible, but also means that it's harder
    // for SND to change the signing algorithm.
    options.TokenValidationParameters.ValidAlgorithms = ["PS256"];
});

builder.Services.AddSingleton<ILockService, InProcessLockService>();
builder.Services.AddTransient<FileService>();

//builder.Services.AddSingleton<IStorageService, InMemoryStorageService>();
builder.Services.AddTransient<IStorageService, FileSystemStorageService>();

var app = builder.Build();

app.UseExceptionHandler("/error");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

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
