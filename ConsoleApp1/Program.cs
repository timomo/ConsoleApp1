using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Upload;

using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

using MimeTypes;

namespace ConsoleApp1
{
    class Program
    {
        static readonly string[] Scopes = { DriveService.Scope.Drive };
        static readonly string ApplicationName = "Drive API .NET Quickstart";
        static DriveService Service;
        static readonly string ParentFolder = "GitHub2GDrive";

        static void Main(string[] args)
        {
            // see: https://developers.google.com/drive/api/v3/quickstart/dotnet

            UserCredential credential;

            string Path = args[0];
            FileInfo file = new FileInfo(Path);
            
            if (! file.Exists)
            {
                Console.WriteLine("not found token file!:" + Path);
                Environment.Exit(1);
            }

            DirectoryInfo di = new DirectoryInfo(args[1]);

            if (! di.Exists)
            {
                Console.WriteLine("not found sync directory!:" + di.FullName);
                Environment.Exit(1);
            }

            string CredPath = System.IO.Path.Combine(file.DirectoryName, "token.json");
            string ClientRootDirectory = di.FullName;

            using (FileStream stream =
                new FileStream(Path, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(CredPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + CredPath);
            }

            Service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            DirectoryInfo directoryInfo = new DirectoryInfo(ClientRootDirectory);

            FilesResource.ListRequest listRequest = Service.Files.List();
            listRequest.Q = $"name = '{ParentFolder}'";
            listRequest.Fields = "files(id, name, parents)";
            FileList result = listRequest.Execute();

            if (result.Files.Count == 0)
            {
                Console.WriteLine("not found!");
                Environment.Exit(1);
            }

            WalkDirectoryTree(directoryInfo, result.Files[0]);
        }

        static void WalkDirectoryTree(System.IO.DirectoryInfo root, Google.Apis.Drive.v3.Data.File CloudParentDirectory)
        {
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;

            try
            {
                files = root.GetFiles("*.*");
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine(e.Message);
            }

            catch (System.IO.DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }

            if (files != null)
            {
                static void Upload_ProgressChanged(IUploadProgress progress)
                {
                    Console.WriteLine(progress.Status + " " + progress.BytesSent + " " + progress.Exception);
                }

                static void Upload_ResponseReceived(Google.Apis.Drive.v3.Data.File file)
                {
                    Console.WriteLine(file.Name + " was uploaded successfully");
                }

                foreach (System.IO.FileInfo fi in files)
                {
                    string MimeType = MimeTypeMap.GetMimeType(fi.Extension);

                    switch (fi.Extension)
                    {
                        case ".pl":
                        case ".pm":
                        case ".cgi":
                        case ".yml":
                        case ".gitignore":
                            MimeType = "text/plain";
                            break;
                    }

                    using FileStream uploadStream = System.IO.File.OpenRead(fi.FullName);

                    Google.Apis.Drive.v3.Data.File driveFile = new Google.Apis.Drive.v3.Data.File
                    {
                        Name = fi.Name,
                        Parents = new List<string>() { CloudParentDirectory.Id },
                        MimeType = MimeType,
                    };

                    Console.WriteLine(driveFile.Name + ":" + driveFile.MimeType);

                    FilesResource.CreateMediaUpload insertRequest = Service.Files.Create(driveFile, uploadStream, MimeType);

                    insertRequest.ProgressChanged += Upload_ProgressChanged;
                    insertRequest.ResponseReceived += Upload_ResponseReceived;

                    insertRequest.Upload();
                }

                subDirs = root.GetDirectories();

                foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                {
                    if (dirInfo.Name == ".git")
                    {
                        continue;
                    }
                    
                    // Check Exists Directory
                    FilesResource.ListRequest listRequest = Service.Files.List();
                    listRequest.Q = $"'{CloudParentDirectory.Id}' in parents and name = '${dirInfo.Name}' and mimeType = 'application/vnd.google-apps.folder'";
                    listRequest.Fields = "files(id, name, parents)";
                    listRequest.PageSize = 1;
                    FileList list = listRequest.Execute();
                    Google.Apis.Drive.v3.Data.File CloudSubDirectory;

                    if (list.Files.Count == 0)
                    {
                        Google.Apis.Drive.v3.Data.File metaData = new Google.Apis.Drive.v3.Data.File()
                        {
                            Name = dirInfo.Name,
                            Parents = new List<string>() { CloudParentDirectory.Id },
                            MimeType = "application/vnd.google-apps.folder",
                        };
                        CloudSubDirectory = Service.Files.Create(metaData).Execute();
                    }
                    else
                    {
                        CloudSubDirectory = list.Files[0];
                    }

                    WalkDirectoryTree(dirInfo, CloudSubDirectory);
                }
            }
        }
    }
}
