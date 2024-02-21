using DatasetFileUpload.Services;
using DatasetFileUpload.Services.Auth;
using DatasetFileUpload.Services.Lock;
using DatasetFileUpload.Services.Storage;
using DatasetFileUpload.Services.Storage.Disk;
using DatasetFileUpload.Services.Storage.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

//builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
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

builder.Services.AddAuthentication().AddJwtBearer(options =>
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateAudience = false,
        ValidateIssuer = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                builder.Configuration.GetSection("AppSettings:SigningKey").Value!
            )
        )
    }
);

builder.Services.AddAuthorization();

builder.Services.AddSingleton<ILockService, InProcessLockService>();
builder.Services.AddTransient<FileService>();

builder.Services.AddSingleton<IStorageService, InMemoryStorageService>();
//builder.Services.AddTransient<IStorageService, FileSystemStorageService>();

builder.Services.AddOptions<FileSystemStorageServiceConfiguration>()
    .Bind(builder.Configuration.GetSection(FileSystemStorageServiceConfiguration.ConfigurationSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();
app.UseAuthentication();

app.MapControllers();

// Generate service token and print it to the terminal (for testing purposes)
if(app.Environment.IsDevelopment())
{
    TokenService tokenService = new(builder.Configuration);
    Console.WriteLine("ServiceToken:");
    Console.WriteLine(tokenService.GetServiceToken());
    Console.WriteLine("");
}


app.Run();
