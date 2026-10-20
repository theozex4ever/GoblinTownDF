namespace Goblins.Sim;

/// <summary>The one seeded RNG. Every random draw in the sim goes through it (ADR 0003). SplitMix64.</summary>
public sealed class SimRandom
{
	public SimRandom(int seed)
	{
		State = (ulong)seed;
	}

	// Parameterless constructor for JSON deserialisation.
	public SimRandom()
	{
	}

	public ulong State { get; set; }

	public ulong NextUInt64()
	{
		var z = State += 0x9E3779B97F4A7C15UL;
		z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
		z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
		return z ^ (z >> 31);
	}

	/// <summary>A number in [0, maxExclusive).</summary>
	public int NextInt(int maxExclusive)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
		return (int)(((NextUInt64() >> 32) * (ulong)maxExclusive) >> 32);
	}
}
