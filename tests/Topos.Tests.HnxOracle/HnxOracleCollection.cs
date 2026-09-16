namespace Topos.Tests.HnxOracle;

/// <summary>
/// Serializes the parity tests, mirroring <c>Topos.Tests.GdsOracle</c>'s own collection. Each
/// request is a container start, so running them in parallel would multiply Docker's startup cost
/// rather than hide it.
/// </summary>
[CollectionDefinition("HnxOracle", DisableParallelization = true)]
public class HnxOracleCollection;
