using System.Data;

#if NETFRAMEWORK
using Microsoft.Data.SqlClient;

using System.Threading;

using NUnit.Framework;

using SqlServiceBrokerListener;

using Assert = NUnit.Framework.Assert;

namespace ServiceBrokerListener.UnitTests
{
    [TestFixture]
    public class SqlDependencyTest
    {
        private const string TEST_DATABASE_NAME = "TestDatabase";

        private const string TEST_TABLE_NAME = "TestTable";

        private const string TEST_TABLE_NAME_2 = "TestTable2";

        private const string MASTER_CONNECTION_STRING =
            "Data Source=(local);Initial Catalog=master;Integrated Security=True";

        private const string TEST_CONNECTION_STRING =
            "Data Source=(local);Initial Catalog=TestDatabase;Integrated Security=True";

        private const string INSERT_FORMAT =
            "USE [" + TEST_DATABASE_NAME + "] INSERT INTO [{1}] VALUES({0})";

        [SetUp]
        public void TestSetup()
        {
            const string CreateDatabaseScript = @"
                CREATE DATABASE " + TEST_DATABASE_NAME;
            const string CreateTableScript = @"                
                USE [" + TEST_DATABASE_NAME + @"]
                CREATE TABLE " + TEST_TABLE_NAME + @" (TestField int)   
                CREATE TABLE " + TEST_TABLE_NAME_2 + @" (TestField int)   
                ALTER DATABASE [" + TEST_DATABASE_NAME + @"] SET ENABLE_BROKER;   
                ALTER AUTHORIZATION ON DATABASE::[" + TEST_DATABASE_NAME + @"] TO [sa]
                ALTER DATABASE [" + TEST_DATABASE_NAME + @"] SET TRUSTWORTHY ON;    
                ";
            TestCleanup();
            ExecuteNonQuery(CreateDatabaseScript, MASTER_CONNECTION_STRING);
            ExecuteNonQuery(CreateTableScript, MASTER_CONNECTION_STRING);
        }

        [TearDown]
        public void TestCleanup()
        {
            const string DropTestDatabaseScript = @"
                IF EXISTS(select * from sys.databases where name='" + TEST_DATABASE_NAME + @"')
                BEGIN
                    ALTER DATABASE [" + TEST_DATABASE_NAME
                                                  + @"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [" + TEST_DATABASE_NAME + @"]
                END
                ";
            ExecuteNonQuery(DropTestDatabaseScript, MASTER_CONNECTION_STRING);
        }

        [Test]
        public void NotificationTestWith1Change()
        {
            NotificationTest(1);
        }

        [Test]
        public void NotificationTestWith5Changes()
        {
            NotificationTest(5);
        }

        [Test]
        public void NotificationTestWith10Changes()
        {
            NotificationTest(10);
        }

        [Test]
        public void ResourcesReleasabilityTestWith1Changes_PROOF_OF_FAIL()
        {
            ResourcesReleasabilityTest(1);
        }

        [Test]
        public void ResourcesReleasabilityTestWith5Changes_PROOF_OF_FAIL()
        {
            ResourcesReleasabilityTest(5);
        }

        [Test]
        public void ResourcesReleasabilityTestWith10Changes_PROOF_OF_FAIL()
        {
            ResourcesReleasabilityTest(1);
        }

        [Test]
        public void TwoTablesNotificationTest()
        {
            const int ChangesCountFirstTable = 5;
            const int ChangesCountSecondTable = 7;

            int changesReceived1 = 0;
            int changesReceived2 = 0;

            OnChangeEventHandler onChange1 = null;
            onChange1 = (s, e) =>
                {
                    if (e != null && e.Info == SqlNotificationInfo.Insert) changesReceived1++;

                    using (SqlConnection connection = new SqlConnection(TEST_CONNECTION_STRING))
                    using (var command1 = new SqlCommand("SELECT TestField FROM dbo.TestTable", connection))
                    {
                        connection.Open();

                        SqlDependency dep = (SqlDependency)s;
                        if (dep != null) dep.OnChange -= onChange1;
                        command1.Notification = null;
                        dep = new SqlDependency(command1);
                        dep.OnChange += onChange1;
                        command1.ExecuteReader().Close();
                    }
                };

            OnChangeEventHandler onChange2 = null;
            onChange2 = (s, e) =>
            {
                if (e != null && e.Info == SqlNotificationInfo.Insert) changesReceived2++;

                using (SqlConnection connection = new SqlConnection(TEST_CONNECTION_STRING))
                using (SqlCommand command2 = new SqlCommand("SELECT TestField FROM dbo.TestTable2", connection))
                {
                    connection.Open();

                    SqlDependency dep = (SqlDependency)s;
                    if (dep != null) dep.OnChange -= onChange2;
                    command2.Notification = null;
                    dep = new SqlDependency(command2);
                    dep.OnChange += onChange2;
                    command2.ExecuteReader().Close();
                }
            };

            SqlDependency.Start(TEST_CONNECTION_STRING);

            onChange1(null, null);

            SqlDependency.Start(TEST_CONNECTION_STRING);

            onChange2(null, null);

            MakeTableInsertChange(ChangesCountFirstTable);
            MakeTableInsertChange(ChangesCountSecondTable, TEST_TABLE_NAME_2);
			
			Assert.That(changesReceived1, Is.EqualTo(ChangesCountFirstTable));
            Assert.That(changesReceived2, Is.EqualTo(ChangesCountSecondTable));

            SqlDependency.Stop(TEST_CONNECTION_STRING);
        }

        public void ResourcesReleasabilityTest(int changesCount)
        {
            using (var sqlConnection = new SqlConnection(TEST_CONNECTION_STRING))
            {
                sqlConnection.Open();

                int sqlConversationEndpointsCount = sqlConnection.GetUnclosedConversationEndpointsCount();
                int sqlConversationGroupsCount = sqlConnection.GetConversationGroupsCount();
                int sqlServiceQueuesCount = sqlConnection.GetServiceQueuesCount();
                int sqlServicesCount = sqlConnection.GetServicesCount();

                NotificationTest(changesCount, false);

                // Microsoft SqlDependency REMOVES queue and service after use.
                Assert.That(sqlServicesCount, Is.EqualTo(sqlConnection.GetServicesCount()));
                Assert.That(
                    sqlServiceQueuesCount,
                    Is.EqualTo(sqlConnection.GetServiceQueuesCount()));

                // Microsoft SqlDependency KEEPS conversation group and endpoint in DB after use.
                // This behavior leads to GIANT memory leaks in SQL Server.
                Assert.That(
                    sqlConversationGroupsCount,
                    Is.Not.EqualTo(sqlConnection.GetConversationGroupsCount()));
                Assert.That(
                    sqlConversationEndpointsCount,
                    Is.Not.EqualTo(sqlConnection.GetUnclosedConversationEndpointsCount()));
            }
        }

        public void NotificationTest(int changesCount, bool useAssert = true)
        {
            SqlDependency.Stop(TEST_CONNECTION_STRING);
            SqlDependency.Start(TEST_CONNECTION_STRING);

            using (SqlConnection connection = new SqlConnection(TEST_CONNECTION_STRING))
            using (SqlCommand command = new SqlCommand("SELECT TestField FROM dbo.TestTable", connection))
            {
                connection.Open();
                int changesReceived = 0;

                OnChangeEventHandler onChange = null;
                onChange = (s, e) =>
                    {
                        if (e.Info == SqlNotificationInfo.Insert) changesReceived++;

                        // SqlDependency magic to receive events consequentially.
                        SqlDependency dep = (SqlDependency)s;
                        dep.OnChange -= onChange;
                        command.Notification = null;
                        dep = new SqlDependency(command);
                        dep.OnChange += onChange;
                        command.ExecuteReader().Close();
                    };

                // Create a dependency and associate it with the SqlCommand.
                SqlDependency dependency = new SqlDependency(command);
                // Subscribe to the SqlDependency event.
                dependency.OnChange += onChange;
                // Execute the command.
                command.ExecuteReader().Close();

                MakeTableInsertChange(changesCount);

                if (useAssert) Assert.That(changesReceived, Is.EqualTo(changesCount));
            }

            SqlDependency.Stop(TEST_CONNECTION_STRING);
        }

        private static void ExecuteNonQuery(string commandText, string connectionString)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(commandText, conn))
            {
                conn.Open();
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
            }
        }

        private static void MakeTableInsertChange(int changesCount, string tableName = TEST_TABLE_NAME)
        {
            for (int i = 0; i < changesCount; i++)
            {
                ExecuteNonQuery(string.Format(INSERT_FORMAT, i, tableName), TEST_CONNECTION_STRING);
                // It is one of weaknesses of Microsoft SqlDependency:
                // you must subscribe on OnChange again after every event firing.
                // Thus you may loose many table changes.
                // In this case we should wait a little bit to give enough time for resubscription.
                Thread.Sleep(500);
            }
        }
    }
}
#else
using NUnit.Framework;

namespace ServiceBrokerListener.UnitTests
{
    [TestFixture]
    public class SqlDependencyTest
    {
        [Test]
        public void SqlDependency_NotSupported_OnNetCore()
        {
            Assert.Ignore("SqlDependency is not supported on .NET Core/.NET (net8+) and this test runs only on .NET Framework.");
        }
    }
}
#endif
