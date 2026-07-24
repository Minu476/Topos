namespace Topos.Tests.GdsOracle;

/// <summary>
/// All GDS-oracle tests share one physical Neo4j database and unqualified Handle numbering
/// (every test starts a fresh <c>HypergraphKernel</c> at Index 0) — xUnit's default
/// parallel-across-classes execution would let two test classes' node sets collide in the same
/// shared graph, corrupting each other's results. Same fix RLB's own Neo4j integration tests use
/// (`RichLearning.V2.Tests/Neo4jGraphMemoryIntegrationTests.cs`:
/// <c>[CollectionDefinition("Neo4j Integration", DisableParallelization = true)]</c>): force every
/// class in this project onto one collection so they run sequentially against the oracle.
/// </summary>
[CollectionDefinition("GDS Oracle", DisableParallelization = true)]
public sealed class GdsOracleCollectionDefinition;
