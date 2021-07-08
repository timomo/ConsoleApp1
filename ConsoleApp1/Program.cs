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
            string Path = @"D:\Users\tyomo\Downloads\client_secret_433314261858-bm3meh16gj7thgbg8sg6qpdvssf2prdu.apps.googleusercontent.com.json";
            string CredPath = @"D:\Users\tyomo\Downloads\token.json";
            string ClientRootDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), @"test\ffadventure2");

            using (FileStream stream =
                new FileStream(Path, FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
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

            // First, process all the files directly under this folder
            try
            {
                files = root.GetFiles("*.*");
            }
            // This is thrown if even one of the files requires permissions greater
            // than the application provides.
            catch (UnauthorizedAccessException e)
            {
                // This code just writes out the message and continues to recurse.
                // You may decide to do something different here. For example, you
                // can try to elevate your privileges and access the file again.
                // log.Add(e.Message);
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
                    // In this example, we only access the existing FileInfo object. If we
                    // want to open, delete or modify the file, then
                    // a try-catch block is required here to handle the case
                    // where the file has been deleted since the call to TraverseTree().

                    string MimeType = MimeTypeMap.GetMimeType(fi.Extension);

                    if (fi.Extension == ".pl" || fi.Extension == ".pm" || fi.Extension == ".cgi")
                    {
                        MimeType = "text/plain";
                    }

                    using FileStream uploadStream = System.IO.File.OpenRead(fi.FullName);

                    Google.Apis.Drive.v3.Data.File driveFile = new Google.Apis.Drive.v3.Data.File
                    {
                        Name = fi.Name,
                        Parents = new List<string>() { CloudParentDirectory.Id },
                        MimeType = MimeType,
                    };

                    Console.WriteLine(driveFile.Name + ":" + driveFile.MimeType);

                    // Get the media upload request object.
                    FilesResource.CreateMediaUpload insertRequest = Service.Files.Create(driveFile, uploadStream, MimeType);

                    // Add handlers which will be notified on progress changes and upload completion.
                    // Notification of progress changed will be invoked when the upload was started,
                    // on each upload chunk, and on success or failure.
                    insertRequest.ProgressChanged += Upload_ProgressChanged;
                    insertRequest.ResponseReceived += Upload_ResponseReceived;

                    insertRequest.Upload();
                }

                // Now find all the subdirectories under this directory.
                subDirs = root.GetDirectories();

                foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                {
                    if (dirInfo.Name == ".git")
                    {
                        continue;
                    }
                    // Resursive call for each subdirectory.

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
