using AdoNet.Specification.Tests;

namespace Npgsql.Specification.Tests
{
    public sealed class NpgsqlDataReaderTests : DataReaderTestBase<NpgsqlDbFactoryFixture>
	{
		public NpgsqlDataReaderTests(NpgsqlDbFactoryFixture fixture)
			: base(fixture)
		{
		}
	}
}
