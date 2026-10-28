using System.Reflection;
using System.Text.RegularExpressions;
using Goblins.Sim;
using Xunit;

namespace Goblins.Sim.Tests;

public partial class ArchitectureTests
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

	// The five goblin Task arrays: only GoblinTasks changes them (see the sim-architecture skill).
	const string TaskFields = "GoblinTask|GoblinJobTile|GoblinWanderTile|GoblinWanderTicks|GoblinStepTicks";

	// `x.Field[i] = v`, `+=`, `++`, `--` on an element, and Array.Fill/Clear/Copy or `x.Field = ...` on a whole array.
	// `==` is a comparison and `=>` a lambda, not assignments.
	[GeneratedRegex($@"\.(?<field>{TaskFields})\s*\[[^\]]*\]\s*(=(?![=>])|[-+*/%&|^]=|\+\+|--)"
		+ $@"|(\+\+|--)\s*\w+\.(?<field>{TaskFields})\s*\["
		+ $@"|\bArray\.(Fill|Clear|Copy|Reverse|Sort)\s*\(\s*\w+\.(?<field>{TaskFields})\b"
		+ $@"|\.(?<field>{TaskFields})\s*=(?![=>])")]
	private static partial Regex TaskFieldWrite();

	// Fields each file may write besides GoblinTasks.cs: JobSystem's movement counts step ticks,
	// incrementing them and resetting them after each step.
	static readonly Dictionary<string, string[]> Exceptions = new()
	{
		["JobSystem.cs"] = ["GoblinStepTicks"],
	};

	static IEnumerable<string> StrayTaskWrites(string fileName, string source)
	{
		if (fileName == "GoblinTasks.cs")
			yield break;

		var allowed = Exceptions.GetValueOrDefault(fileName, []);
		var lines = source.Split('\n');
		for (var i = 0; i < lines.Length; i++)
		{
			var code = lines[i];
			var comment = code.IndexOf("//", StringComparison.Ordinal);
			if (comment >= 0)
				code = code[..comment];
			foreach (Match write in TaskFieldWrite().Matches(code))
				if (!allowed.Contains(write.Groups["field"].Value))
					yield return $"{fileName}:{i + 1}: {lines[i].Trim()}";
		}
	}

	static string SimSourceDirectory()
	{
		for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
		{
			var src = Path.Combine(dir.FullName, "sim", "src");
			if (File.Exists(Path.Combine(src, "Goblins.Sim.csproj")))
				return src;
		}
		throw new DirectoryNotFoundException("sim/src not found above " + AppContext.BaseDirectory);
	}

	[Fact]
	public void Only_GoblinTasks_writes_the_goblin_task_arrays()
	{
		var strays = Directory.EnumerateFiles(SimSourceDirectory(), "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
				&& !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
			.SelectMany(f => StrayTaskWrites(Path.GetFileName(f), File.ReadAllText(f)))
			.ToList();

		Assert.True(strays.Count == 0,
			"Goblin Task fields change only through GoblinTasks:\n" + string.Join("\n", strays));
	}

	[Theory]
	[InlineData("s.GoblinTask[g] = (byte)GoblinTask.Idle;")]
	[InlineData("s.GoblinJobTile[goblin] = -1;")]
	[InlineData("s.GoblinWanderTile[g] = target;")]
	[InlineData("s.GoblinWanderTicks[g] -= 1;")]
	[InlineData("if (--s.GoblinWanderTicks[g] == 0) Go();")]
	[InlineData("s.GoblinStepTicks[g]++;")]
	[InlineData("Array.Fill(s.GoblinTask, (byte)0);")]
	[InlineData("s.GoblinJobTile = new int[30];")]
	public void The_scan_flags_a_stray_write_outside_GoblinTasks(string line)
	{
		Assert.Single(StrayTaskWrites("SomeSystem.cs", line));
	}

	[Theory]
	[InlineData("if (s.GoblinTask[g] == 0) return;")]
	[InlineData("var job = s.GoblinJobTile[g];")]
	[InlineData("if ((GoblinTask)s.GoblinTask[g] == GoblinTask.Idle) return;")]
	[InlineData("// s.GoblinTask[g] = 0;")]
	[InlineData("public byte[] GoblinTask { get; set; } = new byte[MaxGoblins];")]
	public void The_scan_ignores_reads_comments_and_declarations(string line)
	{
		Assert.Empty(StrayTaskWrites("SomeSystem.cs", line));
	}

	[Fact]
	public void The_scan_allows_GoblinTasks_and_the_step_tick_count_in_JobSystem_only()
	{
		Assert.Empty(StrayTaskWrites("GoblinTasks.cs", "s.GoblinTask[g] = 0;"));
		Assert.Empty(StrayTaskWrites("JobSystem.cs", "if (++s.GoblinStepTicks[g] < 3) return; s.GoblinStepTicks[g] = 0;"));
		Assert.NotEmpty(StrayTaskWrites("JobSystem.cs", "s.GoblinTask[g] = 0;"));
		Assert.NotEmpty(StrayTaskWrites("Simulation.cs", "s.GoblinStepTicks[g] = 0;"));
	}
}
