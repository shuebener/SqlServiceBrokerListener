using NUnit.Framework;

using SqlServiceBrokerListener;

namespace ServiceBrokerListener.UnitTests
{
	[TestFixture]
	public class SqlDependencyExValidationTest
	{
		// These tests never open a connection, so a dummy connection string is enough.
		private const string CONNECTION_STRING = "Data Source=localhost;User ID=unused;Password=unused;TrustServerCertificate=True;";

		[TestCase("TestDatabase", "TestTable", "dbo")]
		[TestCase("Test Database", "Order", "temp")]
		[TestCase("My-Database", "My Table", "_schema1")]
		[TestCase("Datenbank_Ü", "Tabelle_Ä", "Schema_Ö")]
		public void ValidNames_AreAccepted(string database, string table, string schema)
		{
			Assert.DoesNotThrow(() => new SqlDependencyEx(CONNECTION_STRING, database, table, schema));
		}

		[TestCase("db'; DROP TABLE x; --")]
		[TestCase("db]; DROP TABLE x; --")]
		[TestCase("db[")]
		[TestCase("")]
		public void InvalidDatabaseName_IsRejected(string database)
		{
			Assert.Throws<ArgumentException>(() => new SqlDependencyEx(CONNECTION_STRING, database, "TestTable"));
		}

		[TestCase("t'; DROP TABLE x; --")]
		[TestCase("t]; DROP TABLE x; --")]
		[TestCase("t\r\nGO")]
		[TestCase("")]
		public void InvalidTableName_IsRejected(string table)
		{
			Assert.Throws<ArgumentException>(() => new SqlDependencyEx(CONNECTION_STRING, "TestDatabase", table));
		}

		[TestCase("dbo; DROP TABLE x")]
		[TestCase("dbo.other")]
		[TestCase("my schema")]
		[TestCase("1schema")]
		[TestCase("sch'ema")]
		[TestCase("[dbo]")]
		[TestCase("")]
		public void InvalidSchemaName_IsRejected(string schema)
		{
			Assert.Throws<ArgumentException>(() => new SqlDependencyEx(CONNECTION_STRING, "TestDatabase", "TestTable", schema));
		}

		[Test]
		public void NullNames_AreRejected()
		{
			Assert.Throws<ArgumentNullException>(() => new SqlDependencyEx(CONNECTION_STRING, null!, "TestTable"));
			Assert.Throws<ArgumentNullException>(() => new SqlDependencyEx(CONNECTION_STRING, "TestDatabase", null!));
			Assert.Throws<ArgumentNullException>(() => new SqlDependencyEx(CONNECTION_STRING, "TestDatabase", "TestTable", null!));
		}

		[Test]
		public void NameLongerThan128Characters_IsRejected()
		{
			Assert.Throws<ArgumentException>(() => new SqlDependencyEx(CONNECTION_STRING, new string('a', 129), "TestTable"));
		}

		[Test]
		public void StaticMethods_RejectInvalidDatabaseName()
		{
			Assert.Throws<ArgumentException>(() => SqlDependencyEx.CleanDatabase(CONNECTION_STRING, "db'; DROP TABLE x; --"));
			Assert.Throws<ArgumentException>(() => SqlDependencyEx.GetDependencyDbIdentities(CONNECTION_STRING, "db]x"));
		}

		[Test]
		public void NewListener_HasNoLastError()
		{
			var listener = new SqlDependencyEx(CONNECTION_STRING, "TestDatabase", "TestTable");

			Assert.That(listener.LastError, Is.Null);
		}
	}
}
