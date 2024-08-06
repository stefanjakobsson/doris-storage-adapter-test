using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace DorisStorageAdapter.Controllers.Attributes;

internal class BinaryRequestBodyFilter : IOperationFilter
{
    /// <summary>
    /// Configures operations decorated with the <see cref="BinaryRequestBodyAttribute" />.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="context">The context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttributes(typeof(BinaryRequestBodyAttribute), false).FirstOrDefault()
            is not BinaryRequestBodyAttribute attribute)
        {
            return;
        }

        operation.RequestBody = new() { Required = true };
        operation.RequestBody.Content.Add(attribute.ContentType, new()
        {
            Schema = new()
            {
                Type = "string",
                Format = "binary",
            },
        });
    }
}
