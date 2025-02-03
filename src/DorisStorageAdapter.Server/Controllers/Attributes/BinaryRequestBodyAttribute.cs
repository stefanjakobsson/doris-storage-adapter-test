using System;

namespace DorisStorageAdapter.Server.Controllers.Attributes;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class BinaryRequestBodyAttribute(string contentType) : Attribute
{
    public string ContentType { get; } = contentType;
}