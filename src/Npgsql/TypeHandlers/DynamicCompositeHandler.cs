#region License
// The PostgreSQL License
//
// Copyright (C) 2017 The Npgsql Development Team
//
// Permission to use, copy, modify, and distribute this software and its
// documentation for any purpose, without fee, and without a written
// agreement is hereby granted, provided that the above copyright notice
// and this paragraph and the following two paragraphs appear in all copies.
//
// IN NO EVENT SHALL THE NPGSQL DEVELOPMENT TEAM BE LIABLE TO ANY PARTY
// FOR DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES,
// INCLUDING LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
// DOCUMENTATION, EVEN IF THE NPGSQL DEVELOPMENT TEAM HAS BEEN ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//
// THE NPGSQL DEVELOPMENT TEAM SPECIFICALLY DISCLAIMS ANY WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
// ON AN "AS IS" BASIS, AND THE NPGSQL DEVELOPMENT TEAM HAS NO OBLIGATIONS
// TO PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
#endregion

#if !NETSTANDARD1_3

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Npgsql.BackendMessages;
using Npgsql.PostgresTypes;
using Npgsql.TypeHandling;
using Npgsql.TypeMapping;
using NpgsqlTypes;

namespace Npgsql.TypeHandlers
{
    /// <summary>
    /// Type handler for PostgreSQL composite types, mapping them to C# dynamic.
    /// This is the default handler used for composites.
    /// </summary>
    /// <seealso cref="StaticCompositeHandler{T}"/>.
    /// <remarks>
    /// http://www.postgresql.org/docs/current/static/rowtypes.html
    ///
    /// Encoding:
    /// A 32-bit integer with the number of columns, then for each column:
    /// * An OID indicating the type of the column
    /// * The length of the column(32-bit integer), or -1 if null
    /// * The column data encoded as binary
    /// </remarks>
    class DynamicCompositeHandler : NpgsqlTypeHandler<dynamic>
    {
        readonly ConnectorTypeMapper _typeMapper;

        [CanBeNull]
        List<(string Name, NpgsqlTypeHandler Handler)> _members;

        internal DynamicCompositeHandler(ConnectorTypeMapper typeMapper)
        {
            // After construction the composite handler will have a reference to its PostgresCompositeType,
            // which contains information about the fields. But the actual binding of their type OIDs
            // to their type handlers is done only very late upon first usage of the handler,
            // allowing composite types to be activated in any order regardless of dependencies.

            _typeMapper = typeMapper;
        }

        #region Read

        public override async ValueTask<dynamic> Read(NpgsqlReadBuffer buf, int len, bool async, FieldDescription fieldDescription = null)
        {
            if (_members == null)
                ResolveFields();
            Debug.Assert(_members != null);

            await buf.Ensure(4, async);
            var fieldCount = buf.ReadInt32();
            if (fieldCount != _members.Count)
            {
                // PostgreSQL sanity check
                throw new Exception($"pg_attributes contains {_members.Count} rows for type {PgDisplayName}, but {fieldCount} fields were received!");
            }

            var result = (IDictionary<string, object>)new ExpandoObject();

            foreach (var member in _members)
            {
                await buf.Ensure(8, async);
                buf.ReadInt32();  // read typeOID, not used
                var fieldLen = buf.ReadInt32();
                if (fieldLen == -1)
                {
                    // Null field, simply skip it and leave at default
                    continue;
                }
                result[member.Name] = await member.Handler.ReadAsObject(buf, fieldLen, async);
            }
            return result;
        }

        #endregion

        #region Write

        public override int ValidateAndGetLength(dynamic value, ref NpgsqlLengthCache lengthCache, NpgsqlParameter parameter)
        {
            throw new NotImplementedException();
        }

        public override Task Write(dynamic value, NpgsqlWriteBuffer buf, NpgsqlLengthCache lengthCache, NpgsqlParameter parameter, bool async)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Misc

        void ResolveFields()
        {
            Debug.Assert(PostgresType is PostgresCompositeType, "CompositeHandler initialized with a non-composite type");
            var rawFields = ((PostgresCompositeType)PostgresType).Fields;

            _members = new List<(string, NpgsqlTypeHandler)>(rawFields.Count);
            foreach (var rawField in rawFields)
            {
                if (!_typeMapper.TryGetByOID(rawField.TypeOID, out var handler))
                    throw new Exception($"PostgreSQL composite type {PgDisplayName} has field {rawField.PgName} with an unknown type (TypeOID={rawField.TypeOID})");
                _members.Add((rawField.PgName, handler));
            }
        }

        #endregion
    }

#pragma warning disable CA1040    // Avoid empty interfaces
    interface IDynamicCompositeTypeHandlerFactory { }
#pragma warning restore CA1040    // Avoid empty interfaces

    class DynamicCompositeTypeHandlerFactory : NpgsqlTypeHandlerFactory<dynamic>
    {
        protected override NpgsqlTypeHandler<dynamic> Create(NpgsqlConnection conn)
            => new DynamicCompositeHandler(conn.Connector.TypeMapper);
    }
}

#endif
