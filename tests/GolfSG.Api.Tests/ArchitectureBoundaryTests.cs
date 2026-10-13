using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using GolfSG.Api.Controllers;
using GolfSG.Application.Server;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class ArchitectureBoundaryTests
{
    [TestMethod]
    public void ApiHasNoDirectDatabaseLibraryDependency()
    {
        var references = ReferencedAssemblies(typeof(RoundsController).Assembly);

        Assert.DoesNotContain("Npgsql", references);
        Assert.IsFalse(references.Any(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));
        Assert.DoesNotContain("GolfSG.Database.Setup", references);
    }

    [TestMethod]
    public void ControllersExposeOnlyHttpAndApplicationTypes()
    {
        var controllers = typeof(RoundsController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controller in controllers)
        {
            var signatures = controller.GetConstructors().SelectMany(ctor => ctor.GetParameters().Select(p => p.ParameterType))
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType)));
            foreach (var type in signatures.SelectMany(ExpandType))
            {
                var name = type.Assembly.GetName().Name!;
                Assert.IsFalse(name.StartsWith("Npgsql", StringComparison.Ordinal) ||
                    name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                    name == "GolfSG.Server.Infrastructure", $"{controller.Name} exposes persistence type {type}.");
            }
        }
    }

    [TestMethod]
    public void SharedApplicationAndDeviceInfrastructureRemainIndependentOfServerPersistence()
    {
        foreach (var assembly in new[] { typeof(ServerRoundService).Assembly, typeof(RoundFileStore).Assembly })
        {
            var references = ReferencedAssemblies(assembly);
            Assert.DoesNotContain("GolfSG.Api", references);
            Assert.DoesNotContain("GolfSG.Server.Infrastructure", references);
            Assert.DoesNotContain("GolfSG.Database.Setup", references);
            Assert.DoesNotContain("Npgsql", references);
            Assert.IsFalse(references.Any(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void ServerPersistenceDoesNotDependOnApiOrDeviceInfrastructure()
    {
        var references = ReferencedAssemblies(typeof(AppDbContext).Assembly);

        Assert.Contains("GolfSG.Application", references);
        Assert.DoesNotContain("GolfSG.Api", references);
        Assert.DoesNotContain("GolfSG.Infrastructure", references);
        Assert.DoesNotContain("GolfSG.Database.Setup", references);
    }

    [TestMethod]
    public void DatabaseMigrationsBelongToExplicitSetupTool()
    {
        var api = typeof(RoundsController).Assembly;
        var persistence = typeof(AppDbContext).Assembly;
        // Inspect the setup executable without starting/loading administrative code.
        using var setupFile = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "GolfSG.Database.Setup.dll"));
        using var setupImage = new PEReader(setupFile);
        var setup = setupImage.GetMetadataReader();
        var resources = setup.ManifestResources.Select(handle => setup.GetString(setup.GetManifestResource(handle).Name)).ToArray();
        var references = setup.AssemblyReferences.Select(handle => setup.GetString(setup.GetAssemblyReference(handle).Name)).ToArray();

        Assert.IsFalse(api.GetManifestResourceNames().Any(name => name.EndsWith(".sql", StringComparison.Ordinal)));
        Assert.IsFalse(persistence.GetManifestResourceNames().Any(name => name.EndsWith(".sql", StringComparison.Ordinal)));
        Assert.Contains("GolfSG.Database.Setup.Sql.Migrations.001_initial.sql", resources);
        Assert.Contains("GolfSG.Database.Setup.Sql.Migrations.002_identity_round_ownership.sql", resources);
        Assert.Contains("GolfSG.Database.Setup.Sql.Migrations.003_course_library_games.sql", resources);
        Assert.DoesNotContain("GolfSG.Api", references);
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var element in ExpandType(type.GetElementType()!)) yield return element;
        foreach (var argument in type.GetGenericArguments())
            foreach (var nested in ExpandType(argument)) yield return nested;
    }

    private static HashSet<string> ReferencedAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToHashSet(StringComparer.Ordinal);
}
