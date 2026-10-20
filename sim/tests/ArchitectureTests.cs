using System.Reflection;
using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public class ArchitectureTests
{
	[Fact]
	public void Sim_assembly_references_no_godot_assemblies()
	{
		var sim = typeof(SimAssembly).Assembly;

		var godotRefs = sim.GetReferencedAssemblies()
			.Where(a => a.Name!.StartsWith("Godot", StringComparison.OrdinalIgnoreCase))
			.ToList();

		Assert.Empty(godotRefs);
	}
}
