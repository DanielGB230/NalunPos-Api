using System;

namespace Pos.Application.Common.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PublicUseCaseAttribute : Attribute
{
}
