using System.Data.Common;
using AdoNet.Specification.Tests.Databases;

namespace Npgsql.Specification.Tests
{
	public sealed class NpgsqlDbFactoryFixture : DbFactoryFixtureBase<PostgresDatabase>
	{
		public override DbProviderFactory Factory => NpgsqlFactory.Instance;
	}
}
