using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure;

namespace RoomToRentRadarUnitTests.Architecture;

[TestFixture]
public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly PortsAssembly = typeof(RoomToRentRadar.Ports.Outbound.Repositories.IUserRepository).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(RoomToRentRadar.Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
    private static readonly Assembly DatastoreCosmosAssembly = typeof(RoomToRentRadar.Datastore.CosmosDb.Repositories.CosmosUserRepository).Assembly;
    private static readonly Assembly DatastoreSqliteAssembly = typeof(RoomToRentRadar.Datastore.Sqlite.Repositories.SqliteUserRepository).Assembly;
    private static readonly Assembly ApiAssembly = typeof(RoomToRentRadar.Api.Controllers.AuthController).Assembly;

    private static bool IsCandidateClass(Type t) =>
        t.IsClass &&
        !t.IsAbstract &&
        !t.Name.StartsWith("<>") &&
        !t.IsDefined(typeof(CompilerGeneratedAttribute), false);

    [Test]
    public void CoreAssemblies_MustNotReferenceForbiddenExternalLibraries()
    {
        var forbiddenPrefixes = new[]
        {
            "Microsoft.AspNetCore",
            "Microsoft.Maui",
            "System.Net.Http",
            "Microsoft.IdentityModel",
            "Microsoft.Data.Sqlite",
            "Microsoft.Azure.Cosmos"
        };

        var coreAssemblies = new[] { DomainAssembly, PortsAssembly, ApplicationAssembly };

        foreach (var assembly in coreAssemblies)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var reference in referencedAssemblies)
            {
                foreach (var forbidden in forbiddenPrefixes)
                {
                    Assert.That(
                        reference.Name,
                        Does.Not.StartWith(forbidden),
                        $"Core assembly '{assembly.GetName().Name}' violates Hexagonal Architecture by referencing forbidden library '{reference.Name}'.");
                }
            }
        }
    }

    [Test]
    public void DomainAggregatesAndMutableEntities_MustDeclareConcurrencyTokenProperty()
    {
        var targetTypes = DomainAssembly.GetTypes()
            .Where(t => IsCandidateClass(t) && t.Namespace != null &&
                        (t.Namespace.Contains("Aggregates") || t.Name == "Room"))
            .ToList();

        Assert.That(targetTypes, Is.Not.Empty, "Expected at least one Domain Aggregate or Mutable Entity to be verified.");

        foreach (var type in targetTypes)
        {
            var concurrencyProp = type.GetProperty("ConcurrencyToken", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(
                concurrencyProp,
                Is.Not.Null,
                $"Aggregate/Entity '{type.Name}' must declare a 'ConcurrencyToken' property to support Optimistic Concurrency Control (Guardrail 1.5.2).");
            Assert.That(
                concurrencyProp!.PropertyType,
                Is.EqualTo(typeof(string)),
                $"Aggregate/Entity '{type.Name}' ConcurrencyToken must be of type string.");
        }
    }

    [Test]
    public void DomainEntitiesAndAggregates_MustNotHavePublicParameterlessConstructors()
    {
        var domainTypes = DomainAssembly.GetTypes()
            .Where(t => IsCandidateClass(t) &&
                        (t.Namespace?.Contains("Aggregates") == true || t.Namespace?.Contains("Entities") == true))
            .ToList();

        Assert.That(domainTypes, Is.Not.Empty, "Expected domain entities to be verified.");

        foreach (var type in domainTypes)
        {
            var publicParameterlessCtor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Length == 0);

            Assert.That(
                publicParameterlessCtor,
                Is.Null,
                $"Domain type '{type.Name}' has a public parameterless constructor, violating Invariant Protection (Guardrail 1.5.3). Use parameterized constructors or factory methods.");
        }
    }

    [Test]
    public void RootAppSettings_MustHaveAllMockServicesDisabledByDefault()
    {
        var rootDir = FindRepositoryRoot();
        var appsettingsPath = Path.Combine(rootDir, "RoomToRentRadar.Api", "appsettings.json");

        Assert.That(File.Exists(appsettingsPath), Is.True, $"Could not locate '{appsettingsPath}'.");

        var jsonContent = File.ReadAllText(appsettingsPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        AssertPropertyRecursively(root, propertyName =>
        {
            if (propertyName.StartsWith("UseMock", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        });

        static void AssertPropertyRecursively(JsonElement element, Func<string, bool> isMockPropertyPredicate)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (isMockPropertyPredicate(prop.Name))
                    {
                        Assert.That(
                            prop.Value.GetBoolean(),
                            Is.False,
                            $"Configuration property '{prop.Name}' in root appsettings.json MUST be false by default (Guardrail 1.5.4 Secure-by-Default).");
                    }
                    AssertPropertyRecursively(prop.Value, isMockPropertyPredicate);
                }
            }
        }
    }

    [Test]
    public void InfrastructureDependencyInjection_MustDefaultMockFlagsToFalse()
    {
        var method = typeof(DependencyInjection).GetMethod("AddInfrastructure");
        Assert.That(method, Is.Not.Null, "DependencyInjection.AddInfrastructure method was not found.");

        var parameters = method!.GetParameters();
        var mockParam = parameters.FirstOrDefault(p => p.Name != null && p.Name.Contains("Mock", StringComparison.OrdinalIgnoreCase));

        if (mockParam != null)
        {
            Assert.That(
                mockParam.HasDefaultValue,
                Is.True,
                $"Parameter '{mockParam.Name}' must have a default value.");
            Assert.That(
                mockParam.DefaultValue,
                Is.EqualTo(false),
                $"Parameter '{mockParam.Name}' default value MUST be false to satisfy Secure-by-Default (Guardrail 1.5.4).");
        }
    }

    [Test]
    public void ApiControllers_MustNotReturnRawDomainEntitiesOrDatabaseDocuments()
    {
        var controllerTypes = ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToList();

        var forbiddenReturnNamespaces = new[]
        {
            "RoomToRentRadar.Domain.Aggregates",
            "RoomToRentRadar.Domain.Entities",
            "RoomToRentRadar.Datastore"
        };

        foreach (var controller in controllerTypes)
        {
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var action in actions)
            {
                var returnType = action.ReturnType;
                foreach (var forbiddenNamespace in forbiddenReturnNamespaces)
                {
                    Assert.That(
                        IsTypeOrGenericArgumentFromNamespace(returnType, forbiddenNamespace),
                        Is.False,
                        $"Action '{controller.Name}.{action.Name}' returns raw domain/datastore type '{returnType.Name}'. Responses must be projected through Response DTOs (Guardrail 1.5.6).");
                }
            }
        }

        static bool IsTypeOrGenericArgumentFromNamespace(Type type, string namespacePrefix)
        {
            if (type.Namespace != null && type.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal))
            {
                return true;
            }

            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    if (IsTypeOrGenericArgumentFromNamespace(arg, namespacePrefix))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    [Test]
    public void InboundUseCaseInterfaces_MustHaveSingleExecuteAsyncMethod_EnforcingSrpAndIsp()
    {
        var inboundUseCaseInterfaces = PortsAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.Contains("Inbound.UseCases"))
            .ToList();

        Assert.That(inboundUseCaseInterfaces, Is.Not.Empty, "Expected to find Inbound UseCase interfaces in Ports assembly.");

        foreach (var useCaseInterface in inboundUseCaseInterfaces)
        {
            var methods = useCaseInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(
                methods.Length,
                Is.EqualTo(1),
                $"Inbound UseCase interface '{useCaseInterface.Name}' has {methods.Length} methods. Under SRP and ISP, it MUST define exactly 1 method.");

            Assert.That(
                methods[0].Name,
                Is.EqualTo("ExecuteAsync"),
                $"Inbound UseCase interface '{useCaseInterface.Name}' method must be named 'ExecuteAsync'.");
        }
    }

    [Test]
    public void ApplicationUseCases_MustOnlyExposeExecuteAsync_EnforcingSrp()
    {
        var useCaseTypes = ApplicationAssembly.GetTypes()
            .Where(t => IsCandidateClass(t) && t.Namespace != null && t.Namespace.Contains("UseCases"))
            .ToList();

        Assert.That(useCaseTypes, Is.Not.Empty, "Expected to find Application UseCase classes.");

        foreach (var useCaseType in useCaseTypes)
        {
            var publicMethods = useCaseType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();

            Assert.That(
                publicMethods.Count,
                Is.EqualTo(1),
                $"UseCase '{useCaseType.Name}' defines {publicMethods.Count} public methods ({string.Join(", ", publicMethods.Select(m => m.Name))}). Under SRP, it MUST expose only 1 public execution method.");

            Assert.That(
                publicMethods[0].Name,
                Is.EqualTo("ExecuteAsync"),
                $"UseCase '{useCaseType.Name}' public method must be 'ExecuteAsync'.");
        }
    }

    [Test]
    public void CoreAssemblies_MustNotReferenceLowLevelAssemblies_EnforcingDip()
    {
        var lowLevelAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RoomToRentRadar.Infrastructure",
            "RoomToRentRadar.Datastore.CosmosDb",
            "RoomToRentRadar.Datastore.Sqlite",
            "RoomToRentRadar.Api",
            "RoomToRentRadar"
        };

        var coreAssemblies = new[] { DomainAssembly, PortsAssembly, ApplicationAssembly };

        foreach (var assembly in coreAssemblies)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var reference in referencedAssemblies)
            {
                var isLowLevel = reference.Name != null && lowLevelAssemblyNames.Contains(reference.Name);
                Assert.That(
                    isLowLevel,
                    Is.False,
                    $"Core assembly '{assembly.GetName().Name}' violates Dependency Inversion Principle (DIP) by referencing low-level assembly '{reference.Name}'.");
            }
        }
    }

    [Test]
    public void RepositoryImplementations_MustImplementPortInterfacesConsistently_EnforcingLsp()
    {
        var repoAssemblies = new[] { InfrastructureAssembly, DatastoreCosmosAssembly, DatastoreSqliteAssembly };
        var repoPortType = typeof(RoomToRentRadar.Ports.Outbound.Repositories.IUserRepository);
        var expectedMethodNames = repoPortType.GetMethods().Select(m => m.Name).ToHashSet();

        var repoImplementations = repoAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => IsCandidateClass(t) && repoPortType.IsAssignableFrom(t))
            .ToList();

        Assert.That(repoImplementations.Count, Is.GreaterThanOrEqualTo(3), "Expected at least 3 implementations of IUserRepository (Cosmos, SQLite, InMemory).");

        foreach (var repoImpl in repoImplementations)
        {
            var methods = repoImpl.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)
                .ToHashSet();

            foreach (var expectedMethod in expectedMethodNames)
            {
                Assert.That(
                    methods.Contains(expectedMethod),
                    Is.True,
                    $"Repository implementation '{repoImpl.FullName}' does not declare public method '{expectedMethod}' from '{repoPortType.Name}', violating Liskov Substitution Principle (LSP).");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RoomToRentRadar.slnx")) ||
                File.Exists(Path.Combine(current.FullName, "RoomToRentRadar.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, ".agents")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root directory containing '.agents' or solution file.");
    }
}
