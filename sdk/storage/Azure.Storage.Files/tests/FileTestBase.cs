﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Core.Testing;
using Azure.Storage.Files.Models;
using Azure.Storage.Sas;
using Azure.Storage.Test;
using Azure.Storage.Test.Shared;
using NUnit.Framework;

namespace Azure.Storage.Files.Tests
{
    public class FileTestBase : StorageTestBase
    {
        public static Uri s_invalidUri = new Uri("https://error.file.core.windows.net");

        public FileTestBase(bool async) : this(async, null) { }

        public FileTestBase(bool async, RecordedTestMode? mode = null)
            : base(async, mode)
        {
        }

        public string GetNewShareName() => $"test-share-{Recording.Random.NewGuid()}";
        public string GetNewDirectoryName() => $"test-directory-{Recording.Random.NewGuid()}";
        public string GetNewFileName() => $"test-file-{Recording.Random.NewGuid()}";

        public FileClientOptions GetOptions()
        {
            var options = new FileClientOptions
            {
                Diagnostics = { IsLoggingEnabled = true },
                Retry =
                {
                    Mode = RetryMode.Exponential,
                    MaxRetries = Azure.Storage.Constants.MaxReliabilityRetries,
                    Delay = TimeSpan.FromSeconds(Mode == RecordedTestMode.Playback ? 0.01 : 0.5),
                    MaxDelay = TimeSpan.FromSeconds(Mode == RecordedTestMode.Playback ? 0.1 : 10)
                }
            };
            if (Mode != RecordedTestMode.Live)
            {
                options.AddPolicy(new RecordedClientRequestIdPolicy(Recording), HttpPipelinePosition.PerCall);
            }

            return Recording.InstrumentClientOptions(options);
        }

        public IDisposable GetNewDirectory(out DirectoryClient directory, FileServiceClient service = default)
        {
            IDisposable disposingShare = GetNewShare(out ShareClient share, default, service);
            var directoryName = GetNewDirectoryName();
            directory = InstrumentClient(share.GetDirectoryClient(directoryName));
            _ = directory.CreateAsync().Result;
            return disposingShare;
        }

        public IDisposable GetNewFile(out FileClient file, FileServiceClient service = default, string shareName = default, string directoryName = default, string fileName = default)
        {
            IDisposable disposingShare = GetNewShare(out ShareClient share, shareName, service);
            DirectoryClient directory = InstrumentClient(share.GetDirectoryClient(directoryName ?? GetNewDirectoryName()));
            _ = directory.CreateAsync().Result;

            file = InstrumentClient(directory.GetFileClient(fileName ?? GetNewFileName()));
            _ = file.CreateAsync(maxSize: Constants.MB).Result;

            return disposingShare;
        }

        public FileClientOptions GetFaultyFileConnectionOptions(
            int raiseAt = default,
            Exception raise = default)
        {
            raise = raise ?? new IOException("Simulated connection fault");
            FileClientOptions options = GetOptions();
            options.AddPolicy(new FaultyDownloadPipelinePolicy(raiseAt, raise), HttpPipelinePosition.PerCall);
            return options;
        }

        public FileServiceClient GetServiceClient_SharedKey()
            => InstrumentClient(
                new FileServiceClient(
                    new Uri(TestConfigDefault.FileServiceEndpoint),
                    new StorageSharedKeyCredential(
                        TestConfigDefault.AccountName,
                        TestConfigDefault.AccountKey),
                    GetOptions()));

        public FileServiceClient GetServiceClient_AccountSas(StorageSharedKeyCredential sharedKeyCredentials = default, SasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new FileServiceClient(
                    new Uri($"{TestConfigDefault.FileServiceEndpoint}?{sasCredentials ?? GetNewAccountSasCredentials(sharedKeyCredentials ?? GetNewSharedKeyCredentials())}"),
                    GetOptions()));

        public FileServiceClient GetServiceClient_FileServiceSasShare(string shareName, StorageSharedKeyCredential sharedKeyCredentials = default, SasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new FileServiceClient(
                    new Uri($"{TestConfigDefault.FileServiceEndpoint}?{sasCredentials ?? GetNewFileServiceSasCredentialsShare(shareName, sharedKeyCredentials ?? GetNewSharedKeyCredentials())}"),
                    GetOptions()));

        public FileServiceClient GetServiceClient_FileServiceSasFile(string shareName, string filePath, StorageSharedKeyCredential sharedKeyCredentials = default, SasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new FileServiceClient(
                    new Uri($"{TestConfigDefault.FileServiceEndpoint}?{sasCredentials ?? GetNewFileServiceSasCredentialsFile(shareName, filePath, sharedKeyCredentials ?? GetNewSharedKeyCredentials())}"),
                    GetOptions()));

        public IDisposable GetNewShare(out ShareClient share, string shareName = default, FileServiceClient service = default, IDictionary<string, string> metadata = default)
        {
            service ??= GetServiceClient_SharedKey();
            share = InstrumentClient(service.GetShareClient(shareName ?? GetNewShareName()));
            return new DisposingShare(share, metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        public StorageSharedKeyCredential GetNewSharedKeyCredentials()
            => new StorageSharedKeyCredential(
                TestConfigDefault.AccountName,
                TestConfigDefault.AccountKey);

        public SasQueryParameters GetNewAccountSasCredentials(StorageSharedKeyCredential sharedKeyCredentials = default)
        {
            var builder = new AccountSasBuilder
            {
                Protocol = SasProtocol.None,
                Services = AccountSasServices.Files,
                ResourceTypes = AccountSasResourceTypes.Container,
                StartsOn = Recording.UtcNow.AddHours(-1),
                ExpiresOn = Recording.UtcNow.AddHours(+1),
                IPRange = new SasIPRange(IPAddress.None, IPAddress.None)
            };
            builder.SetPermissions(AccountSasPermissions.Create | AccountSasPermissions.Delete);
            return builder.ToSasQueryParameters(sharedKeyCredentials);
        }

        public SasQueryParameters GetNewFileServiceSasCredentialsShare(string shareName, StorageSharedKeyCredential sharedKeyCredentials = default)
        {
            var builder = new FileSasBuilder
            {
                ShareName = shareName,
                Protocol = SasProtocol.None,
                StartsOn = Recording.UtcNow.AddHours(-1),
                ExpiresOn = Recording.UtcNow.AddHours(+1),
                IPRange = new SasIPRange(IPAddress.None, IPAddress.None)
            };
            builder.SetPermissions(ShareSasPermissions.All);
            return builder.ToSasQueryParameters(sharedKeyCredentials ?? GetNewSharedKeyCredentials());
        }

        public SasQueryParameters GetNewFileServiceSasCredentialsFile(string shareName, string filePath, StorageSharedKeyCredential sharedKeyCredentials = default)
        {
            var builder = new FileSasBuilder
            {
                ShareName = shareName,
                FilePath = filePath,
                Protocol = SasProtocol.None,
                StartsOn = Recording.UtcNow.AddHours(-1),
                ExpiresOn = Recording.UtcNow.AddHours(+1),
                IPRange = new SasIPRange(IPAddress.None, IPAddress.None)
            };
            builder.SetPermissions(FileSasPermissions.All);
            return builder.ToSasQueryParameters(sharedKeyCredentials ?? GetNewSharedKeyCredentials());
        }

        public FileSignedIdentifier[] BuildSignedIdentifiers() =>
            new[]
            {
                new FileSignedIdentifier
                {
                    Id = GetNewString(),
                    AccessPolicy =
                        new FileAccessPolicy
                        {
                            StartsOn =  Recording.UtcNow.AddHours(-1),
                            ExpiresOn =  Recording.UtcNow.AddHours(1),
                            Permissions = "rw"
                        }
                }
            };

        public static void AssertValidStorageFileInfo(StorageFileInfo storageFileInfo)
        {
            Assert.IsNotNull(storageFileInfo.ETag);
            Assert.IsNotNull(storageFileInfo.LastModified);
            Assert.IsNotNull(storageFileInfo.IsServerEncrypted);
            Assert.IsNotNull(storageFileInfo.SmbProperties);
            AssertValidFileSmbProperties(storageFileInfo.SmbProperties);
        }

        public static void AssertValidStorageDirectoryInfo(StorageDirectoryInfo storageDirectoryInfo)
        {
            Assert.IsNotNull(storageDirectoryInfo.ETag);
            Assert.IsNotNull(storageDirectoryInfo.LastModified);
            Assert.IsNotNull(storageDirectoryInfo.SmbProperties);
            AssertValidFileSmbProperties(storageDirectoryInfo.SmbProperties);
        }

        public static void AssertValidFileSmbProperties(FileSmbProperties fileSmbProperties)
        {
            Assert.IsNotNull(fileSmbProperties.FileAttributes);
            Assert.IsNotNull(fileSmbProperties.FilePermissionKey);
            Assert.IsNotNull(fileSmbProperties.FileCreationTime);
            Assert.IsNotNull(fileSmbProperties.FileLastWriteTime);
            Assert.IsNotNull(fileSmbProperties.FileChangeTime);
            Assert.IsNotNull(fileSmbProperties.FileId);
            Assert.IsNotNull(fileSmbProperties.ParentId);
        }

        internal static void AssertPropertiesEqual(FileSmbProperties left, FileSmbProperties right)
        {
            Assert.AreEqual(left.FileAttributes, right.FileAttributes);
            Assert.AreEqual(left.FileCreationTime, right.FileCreationTime);
            Assert.AreEqual(left.FileChangeTime, right.FileChangeTime);
            Assert.AreEqual(left.FileId, right.FileId);
            Assert.AreEqual(left.FileLastWriteTime, right.FileLastWriteTime);
            Assert.AreEqual(left.FilePermissionKey, right.FilePermissionKey);
            Assert.AreEqual(left.ParentId, right.ParentId);
        }

        private class DisposingShare : IDisposable
        {
            public ShareClient ShareClient { get; }

            public DisposingShare(ShareClient share, IDictionary<string, string> metadata)
            {
                share.CreateAsync(metadata: metadata, quotaInGB: 1).Wait();

                ShareClient = share;
            }

            public void Dispose()
            {
                if (ShareClient != null)
                {
                    try
                    {
                        ShareClient.DeleteAsync(default/*, new DeleteSnapshotsOptionType()*/).Wait();
                    }
                    catch
                    {
                        // swallow the exception to avoid hiding another test failure
                    }
                }
            }
        }
    }
}
