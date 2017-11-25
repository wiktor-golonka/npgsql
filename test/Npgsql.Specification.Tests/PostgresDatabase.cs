using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdoNet.Specification.Tests.Databases;

namespace Npgsql.Specification.Tests
{
    public class PostgresDatabase : PostgresDatabaseBase
    {
        public override string ConnectionString => "Host=localhost;Username=npgsql_tests;password=npgsql_tests;";
    }
}
