﻿using System;
using System.IO;
using System.Text;
using Hangfire.Mongo.Database;
using Hangfire.Mongo.Tests.Utils;
using MongoDB.Driver;
using Xunit;

namespace Hangfire.Mongo.Tests
{
    public class MongoContinuationFacts
    {
        [Fact, CleanDatabase]
        public void ContinueWith_Executed_Success()
        {
            // ARRANGE
            var connectionString = "mongodb://localhost";
            var databaseName = "Mongo-Hangfire-Continuation";
            var context = new HangfireDbContext(connectionString, databaseName);
            // Make sure we start from scratch
            context.Database.Client.DropDatabase(databaseName);

            var storageOptions = new MongoStorageOptions
            {
                MigrationOptions = new MongoMigrationOptions
                {
                    Strategy = MongoMigrationStrategy.Drop,
                    BackupStrategy = MongoBackupStrategy.None
                },
                QueuePollInterval = TimeSpan.FromMilliseconds(500)
            };
            var serverOptions = new BackgroundJobServerOptions
            {
                ShutdownTimeout = TimeSpan.FromSeconds(15)
            };
            var mongoClientSettings = MongoClientSettings.FromConnectionString(connectionString);
            JobStorage.Current = new MongoStorage(mongoClientSettings, databaseName, storageOptions);
            using (new BackgroundJobServer(serverOptions))
            using (var stream = new MemoryStream())
            using (var textWriter = new StreamWriter(stream))
            {
                Console.SetOut(textWriter);


                // ACT
                var parentId1 = BackgroundJob.Enqueue(() => Console.Write("parent,"));
                var parentId2 = BackgroundJob.ContinueWith(parentId1, () => Console.Write(parentId1 + ","));
                BackgroundJob.ContinueWith(parentId2, () => HangfireTestJobs.ExecuteContinueWithJob(parentId2, true));
                var signalled = HangfireTestJobs.ContinueWithEvent.WaitOne(TimeSpan.FromSeconds(10));

                textWriter.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                var buffer = new byte[stream.Length];

                stream.Read(buffer, 0, (int) stream.Length);

                var consoleOutput = Encoding.UTF8.GetString(buffer);
                var split = consoleOutput.Split(',');


                // ASSERT
                var parent = split[0];
                var parentId1Expected = split[1];
                var parentId2Expected = split[2]
                    .Replace("\n", "")
                    .Replace("\r", "");
                Assert.True(signalled, "not signalled");
                Assert.Equal("parent", parent);
                Assert.Equal(parentId1, parentId1Expected);
                Assert.Equal(parentId2, parentId2Expected);
            }
        }
    }
}