using System;

namespace DorisStorageAdapter.Controllers.Attributes;

[AttributeUsage(AttributeTargets.Method)]
internal class BinaryRequestBodyAttribute(string contentType) : Attribute
{
    public string ContentType { get; } = contentType;
}