using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Credere.ArchitectureTests;

/// <summary>
/// Build-breaking architecture tests enforcing modular-monolith boundaries.
/// See docs/architecture.md and docs/adr/0001-modular-monolith.md.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly OriginationAsm =
        typeof(Credere.Modules.Origination.IOriginationModule).Assembly;
    private static readonly Assembly ServicingAsm =
        typeof(Credere.Modules.Servicing.IServicingModule).Assembly;
    private static readonly Assembly DocumentsAsm =
        typeof(Credere.Modules.Documents.IDocumentsModule).Assembly;
    private static readonly Assembly ReportingAsm =
        typeof(Credere.Modules.Reporting.IReportingModule).Assembly;
    private static readonly Assembly AiAsm =
        typeof(Credere.Modules.Ai.IAiModule).Assembly;

    public static TheoryData<string, Assembly, string[]> ModuleIsolation()
    {
        var all = new[]
        {
            "Credere.Modules.Origination",
            "Credere.Modules.Servicing",
            "Credere.Modules.Documents",
            "Credere.Modules.Reporting",
            "Credere.Modules.Ai"
        };

        var data = new TheoryData<string, Assembly, string[]>();
        void Add(string name, Assembly asm)
        {
            var others = Array.FindAll(all, n => n != name);
            data.Add(name, asm, others);
        }

        Add("Credere.Modules.Origination", OriginationAsm);
        Add("Credere.Modules.Servicing", ServicingAsm);
        Add("Credere.Modules.Documents", DocumentsAsm);
        Add("Credere.Modules.Reporting", ReportingAsm);
        Add("Credere.Modules.Ai", AiAsm);
        return data;
    }

    [Theory]
    [MemberData(nameof(ModuleIsolation))]
    public void Module_must_not_reference_other_module_namespaces(
        string moduleName,
        Assembly moduleAssembly,
        string[] forbiddenNamespaces)
    {
        var result = Types.InAssembly(moduleAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{moduleName} must not depend on any other module. Offenders: " +
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Core_depends_only_on_BCL_and_NodaTime()
    {
        var result = Types.InAssembly(typeof(Credere.Core.CoreMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Credere.Shared",
                "Credere.Modules.Origination",
                "Credere.Modules.Servicing",
                "Credere.Modules.Documents",
                "Credere.Modules.Reporting",
                "Credere.Modules.Ai",
                "Microsoft.EntityFrameworkCore",
                "MongoDB.Driver",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Credere.Core is the shared kernel: NodaTime only. Offenders: " +
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Shared_depends_only_on_BCL()
    {
        var result = Types.InAssembly(typeof(Credere.Shared.SharedMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Credere.Core",
                "Credere.Modules.Origination",
                "Credere.Modules.Servicing",
                "Credere.Modules.Documents",
                "Credere.Modules.Reporting",
                "Credere.Modules.Ai",
                "NodaTime",
                "Microsoft.EntityFrameworkCore",
                "MongoDB.Driver",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Credere.Shared contains cross-cutting primitives only. Offenders: " +
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
