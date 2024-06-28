using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class TestS3Controller(IStorageService storageService) : Controller
{
    private readonly IStorageService storageService = storageService;

    [HttpGet("/test")]
    public async Task Test()
    {
        await foreach (var file in storageService.ListFiles(""))
        {
        }
    }

}
