using DatasetFileUpload.Authorization;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace DatasetFileUpload.Services.Auth;

internal class TokenService(IConfiguration configuration)
{
    private readonly IConfiguration configuration = configuration;

    public string GetUserToken(
        DatasetVersionIdentifier datasetVersion, 
        IEnumerable<DataAccessScope> scopes, 
        IEnumerable<(string Type, string Value)> claims)
    {
        return GetToken([
            new(Claims.DatasetIdentifier, datasetVersion.DatasetIdentifier),
            new(Claims.DatasetVersionNumber, datasetVersion.VersionNumber),
            new("scope", string.Join(" ", scopes.Distinct())),
            ..claims.Select(c => new Claim(c.Type, c.Value))
        ]);
    }

    public string GetServiceToken()
    {
        return GetToken([
            new("scope", Scope.service)
        ]);
    }

    private string GetToken(IEnumerable<Claim> claims)
    {
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