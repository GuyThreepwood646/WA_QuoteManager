using System.Xml.Linq;

namespace QuoteManager.Architecture.Tests;

/// <summary>
/// Mechanical enforcement of the inward-only dependency direction between projects.
///
/// These assertions read the project files rather than the compiled assemblies, and that choice is
/// deliberate. Roslyn omits assembly references that no code actually uses, so an assembly-level
/// check silently passes while a project is still thin and cannot see a package that has been added
/// but not yet called. The invariant we care about is what a project is *allowed to declare*, which
/// is exactly what the csproj records. This catches a stray `dotnet add package` on the next build.
/// </summary>
public sealed class DependencyRuleTests
{
    /// <summary>Package families that must never appear in the inner layers.</summary>
    private static readonly string[] ForbiddenInCore =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Data",
        "Azure.",
        "Serilog",
        "Scalar",
    ];

    [Fact]
    public void Domain_declares_no_dependencies_at_all()
    {
        var domain = Project("src/QuoteManager.Domain/QuoteManager.Domain.csproj");

        domain.ProjectReferences.ShouldBeEmpty(
            "the Domain sits at the centre of the hexagon and depends on nothing");
        domain.PackageReferences.ShouldBeEmpty(
            "the Domain must stay expressible in plain C# so its rules are testable with no host");
    }

    [Fact]
    public void Application_depends_only_on_the_domain()
    {
        Project("src/QuoteManager.Application/QuoteManager.Application.csproj")
            .ProjectReferences
            .ShouldBe(["QuoteManager.Domain"]);
    }

    [Fact]
    public void Application_declares_no_framework_or_cloud_package()
    {
        Forbidden("src/QuoteManager.Application/QuoteManager.Application.csproj").ShouldBeEmpty();
    }

    [Fact]
    public void Domain_declares_no_framework_or_cloud_package()
    {
        Forbidden("src/QuoteManager.Domain/QuoteManager.Domain.csproj").ShouldBeEmpty();
    }

    [Fact]
    public void Infrastructure_points_inward_and_never_at_the_api()
    {
        Project("src/QuoteManager.Infrastructure/QuoteManager.Infrastructure.csproj")
            .ProjectReferences
            .ShouldBe(["QuoteManager.Application"]);
    }

    [Fact]
    public void Nothing_depends_on_the_api()
    {
        string[] inner =
        [
            "src/QuoteManager.Domain/QuoteManager.Domain.csproj",
            "src/QuoteManager.Application/QuoteManager.Application.csproj",
            "src/QuoteManager.Infrastructure/QuoteManager.Infrastructure.csproj",
        ];

        foreach (var path in inner)
        {
            Project(path).ProjectReferences.ShouldNotContain(
                "QuoteManager.Api",
                $"{path} must not depend on the driving adapter");
        }
    }

    [Fact]
    public void Every_package_version_is_centrally_managed()
    {
        // Central Package Management is what makes the security pins in Directory.Packages.props
        // authoritative. An inline Version attribute would quietly escape that file.
        var offenders = ProjectFiles()
            .Select(file => new
            {
                File = Path.GetFileName(file),
                Inline = XDocument.Load(file)
                    .Descendants("PackageReference")
                    .Where(reference => reference.Attribute("Version") is not null)
                    .Select(reference => reference.Attribute("Include")?.Value ?? "?")
                    .ToList(),
            })
            .Where(entry => entry.Inline.Count > 0)
            .Select(entry => $"{entry.File}: {string.Join(", ", entry.Inline)}")
            .ToList();

        offenders.ShouldBeEmpty();
    }

    private static IReadOnlyList<string> Forbidden(string relativePath) =>
        Project(relativePath).PackageReferences
            .Where(package => ForbiddenInCore.Any(forbidden =>
                package.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private static ProjectManifest Project(string relativePath)
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot.Value, relativePath));

        return new ProjectManifest(
            document.Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(
                    reference.Attribute("Include")!.Value.Replace('\\', '/')))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            document.Descendants("PackageReference")
                .Select(reference => reference.Attribute("Include")!.Value)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList());
    }

    private static IEnumerable<string> ProjectFiles() =>
        Directory.EnumerateFiles(RepositoryRoot.Value, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>
    /// Walks up from the test binaries to the directory holding the solution, so the tests do not
    /// depend on the working directory the runner happens to choose.
    /// </summary>
    private static readonly Lazy<string> RepositoryRoot = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !directory.EnumerateFiles("QuoteManager.slnx").Any())
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the repository root: no QuoteManager.slnx found above the test assembly.");
    });

    private sealed record ProjectManifest(
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyList<string> PackageReferences);
}
