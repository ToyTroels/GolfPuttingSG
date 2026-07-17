using GolfSG.Application.Common;
using GolfSG.Application.Rounds;
using GolfSG.Core;
using GolfSG.Infrastructure.Persistence;

namespace GolfSG.Tests;

[TestClass]
public sealed class ArchitectureBoundaryTests
{
    [TestMethod]
    public void CoreHasNoApplicationInfrastructureOrMauiDependency()
    {
        var references = ReferencedAssemblyNames(typeof(StrokesGainedCalculator).Assembly);

        Assert.DoesNotContain("GolfSG.Application", references);
        Assert.DoesNotContain("GolfSG.Infrastructure", references);
        Assert.IsNull(references.FirstOrDefault(name => name.StartsWith("Microsoft.Maui", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ApplicationDependsOnCoreButNotInfrastructureOrMaui()
    {
        var references = ReferencedAssemblyNames(typeof(RoundApplicationService).Assembly);

        Assert.Contains("GolfSG.Core", references);
        Assert.DoesNotContain("GolfSG.Infrastructure", references);
        Assert.IsNull(references.FirstOrDefault(name => name.StartsWith("Microsoft.Maui", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void InfrastructureDependsOnApplicationAndCoreButNotMaui()
    {
        var references = ReferencedAssemblyNames(typeof(RoundFileStore).Assembly);

        Assert.Contains("GolfSG.Application", references);
        Assert.Contains("GolfSG.Core", references);
        Assert.IsNull(references.FirstOrDefault(name => name.StartsWith("Microsoft.Maui", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SharedActionGateRemainsInPlainApplicationAssembly()
    {
        Assert.AreSame(typeof(RoundApplicationService).Assembly, typeof(AsyncActionGate).Assembly);
    }

    private static HashSet<string> ReferencedAssemblyNames(System.Reflection.Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToHashSet(StringComparer.Ordinal);
}
