using System.IO;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Logging;

namespace DatasetFileUpload.Controllers;

public class AuthController : Controller
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;

    public AuthController(ILogger<AuthController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
    }

    [HttpGet("check")]
    public IActionResult CheckAuthentication()
    {

        // TODO:
        // * check authService if session exists
        // * if not, set head Redirect to login page (config)
        return Ok();
    }
}
