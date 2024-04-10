using DatasetFileUpload.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DatasetFileUpload.Services.Auth;

public class TokenService(IConfiguration configuration)
{
    private readonly IConfiguration configuration = configuration;

    public string GetUploadToken(AuthInfo user, DatasetVersionIdentifier datasetVersion)
    {
        var claims = new List<Claim>
        {
            new("role", "User"),
            new("DatasetIdentifier", datasetVersion.DatasetIdentifier),
            new("VersionNumber", datasetVersion.VersionNumber)
        };

        var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration.GetSection("AppSettings:SigningKey").Value!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddHours(12),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GetServiceToken()
    {
        var claims = new List<Claim>
        {
            new("role", "UploadService")
        };

        var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration.GetSection("AppSettings:SigningKey").Value!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddYears(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}