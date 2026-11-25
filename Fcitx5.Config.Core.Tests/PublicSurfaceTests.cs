using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Fcitx;
using Xunit;

namespace Fcitx5.Config.Core.Tests;

/// <summary>
/// Guards the rule that no public signature mentions a transport type.
/// </summary>
/// <remarks>
/// Swapping the transport stays possible only while nothing public names one, and a signature is
/// put back by accident far more easily than by intent.
/// </remarks>
public class PublicSurfaceTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void NoPublicSignatureNamesATransportType()
    {
        var offenders = typeof(FcitxController).Assembly.GetExportedTypes()
            .SelectMany(owner => SignatureTypes(owner).Select(member => (Owner: owner, member.Name, member.Type)))
            // ToString renders generic arguments, array elements and byrefs, so one check covers
            // every way a type can appear inside a signature.
            .Where(pair => pair.Type.ToString().Contains("Tmds.", StringComparison.Ordinal))
            .Select(pair => $"{pair.Owner.Name}.{pair.Name}: {pair.Type}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheErrorTypesCannotBeBuiltOrExtendedByAConsumer()
    {
        var assembly = typeof(FcitxException).Assembly;

        foreach (var type in assembly.GetExportedTypes().Where(t => typeof(FcitxException).IsAssignableFrom(t)))
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }

        // Abstract with a private protected constructor, so a caller cannot invent a subclass that
        // would pass for a real failure.
        Assert.True(typeof(FcitxException).IsAbstract);
        Assert.DoesNotContain(
            typeof(FcitxException).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            c => c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly);
    }

    /// <summary>Every type a caller can see in <paramref name="type"/>'s signature.</summary>
    private static IEnumerable<(string Name, Type Type)> SignatureTypes(Type type)
    {
        if (type.BaseType is { } baseType)
        {
            yield return ("(base)", baseType);
        }

        foreach (var implemented in type.GetInterfaces())
        {
            yield return ("(interface)", implemented);
        }

        foreach (var method in type.GetMethods(Declared))
        {
            yield return (method.Name, method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                yield return (method.Name, parameter.ParameterType);
            }
        }

        foreach (var constructor in type.GetConstructors(Declared))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return (".ctor", parameter.ParameterType);
            }
        }

        foreach (var property in type.GetProperties(Declared))
        {
            yield return (property.Name, property.PropertyType);
        }

        foreach (var field in type.GetFields(Declared))
        {
            yield return (field.Name, field.FieldType);
        }

        foreach (var @event in type.GetEvents(Declared))
        {
            if (@event.EventHandlerType is { } handler)
            {
                yield return (@event.Name, handler);
            }
        }
    }
}
