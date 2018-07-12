﻿using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Utilities;
using OfficeDevPnP.Core.Framework.Provisioning.Connectors;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.Providers;
using OfficeDevPnP.Core.Framework.Provisioning.Providers.Xml;
using SharePointPnP.PowerShell.CmdletHelpAttributes;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using PnPFileLevel = OfficeDevPnP.Core.Framework.Provisioning.Model.FileLevel;
using SPFile = Microsoft.SharePoint.Client.File;

namespace SharePointPnP.PowerShell.Commands.Provisioning
{
    [Cmdlet(VerbsCommon.Add, "PnPFileToProvisioningTemplate")]
    [CmdletHelp("Adds a file to a PnP Provisioning Template",
        Category = CmdletHelpCategory.Provisioning)]
    [CmdletExample(
       Code = @"PS:> Add-PnPFileToProvisioningTemplate -Path template.pnp -Source $sourceFilePath -Folder $targetFolder",
       Remarks = "Adds a file to a PnP Provisioning Template",
       SortOrder = 1)]
    [CmdletExample(
       Code = @"PS:> Add-PnPFileToProvisioningTemplate -Path template.xml -Source $sourceFilePath -Folder $targetFolder",
       Remarks = "Adds a file reference to a PnP Provisioning XML Template",
       SortOrder = 2)]
    [CmdletExample(
       Code = @"PS:> Add-PnPFileToProvisioningTemplate -Path template.pnp -Source ""./myfile.png"" -Folder ""folderinsite"" -FileLevel Published -FileOverwrite:$false",
       Remarks = "Adds a file to a PnP Provisioning Template, specifies the level as Published and defines to not overwrite the file if it exists in the site.",
       SortOrder = 3)]
    [CmdletExample(
       Code = @"PS:> Add-PnPFileToProvisioningTemplate -Path template.pnp -Source $sourceFilePath -Folder $targetFolder -Container $container",
       Remarks = "Adds a file to a PnP Provisioning Template with a custom container for the file",
       SortOrder = 4)]
    [CmdletExample(
        Code = @"PS:> Add-PnPFileToProvisioningTemplate -Path template.pnp -SourceUrl $urlOfFile",
        Remarks = "Adds a file to a PnP Provisioning Template retrieved from the currently connected web",
        SortOrder = 4)]
    public class AddFileToProvisioningTemplate : PnPWebCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Filename of the .PNP Open XML provisioning template to read from, optionally including full path.")]
        public string Path;

        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "LocalSourceFile", HelpMessage = "The file to add to the in-memory template, optionally including full path.")]
        public string Source;

        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "RemoteSourceFile", HelpMessage = "The file to add to the in-memory template, specifying its url in the current connected Web.")]
        public string SourceUrl;

        [Parameter(Mandatory = true, Position = 2, ParameterSetName = "LocalSourceFile", HelpMessage = "The target Folder for the file to add to the in-memory template.")]
        public string Folder;

        [Parameter(Mandatory = false, Position = 3, HelpMessage = "The target Container for the file to add to the in-memory template, optional argument.")]
        public string Container;

        [Parameter(Mandatory = false, Position = 4, HelpMessage = "The level of the files to add. Defaults to Published")]
        public PnPFileLevel FileLevel = PnPFileLevel.Published;

        [Parameter(Mandatory = false, Position = 5, HelpMessage = "Set to overwrite in site, Defaults to true")]
        public SwitchParameter FileOverwrite = true;

        [Parameter(Mandatory = false, Position = 4, HelpMessage = "Allows you to specify ITemplateProviderExtension to execute while loading the template.")]
        public ITemplateProviderExtension[] TemplateProviderExtensions;

        protected override void ProcessRecord()
        {
            if (!System.IO.Path.IsPathRooted(Path))
            {
                Path = System.IO.Path.Combine(SessionState.Path.CurrentFileSystemLocation.Path, Path);
            }
            // Load the template
            var template = ReadProvisioningTemplate
                .LoadProvisioningTemplateFromFile(Path,
                TemplateProviderExtensions);

            if (template == null)
            {
                throw new ApplicationException("Invalid template file!");
            }
            if (this.ParameterSetName == "RemoteSourceFile")
            {
                SelectedWeb.EnsureProperty(w => w.ServerRelativeUrl);
                var sourceUri = new Uri(SourceUrl, UriKind.RelativeOrAbsolute);
                var serverRelativeUrl =
                    sourceUri.IsAbsoluteUri ? sourceUri.AbsolutePath :
                    SourceUrl.StartsWith("/", StringComparison.Ordinal) ? SourceUrl :
                    SelectedWeb.ServerRelativeUrl.TrimEnd('/') + "/" + SourceUrl;

                var file = SelectedWeb.GetFileByServerRelativeUrl(serverRelativeUrl);

                var fileName = file.EnsureProperty(f => f.Name);
                var folderRelativeUrl = serverRelativeUrl.Substring(0, serverRelativeUrl.Length - fileName.Length - 1);
                var folderWebRelativeUrl = HttpUtility.UrlKeyValueDecode(folderRelativeUrl.Substring(SelectedWeb.ServerRelativeUrl.TrimEnd('/').Length + 1));
                if (ClientContext.HasPendingRequest) ClientContext.ExecuteQuery();
                try
                {
                    using (var fi = SPFile.OpenBinaryDirect(ClientContext, serverRelativeUrl))
                    using (var ms = new MemoryStream())
                    {
                        // We are using a temporary memory stream because the file connector is seeking in the stream
                        // and the stream provided by OpenBinaryDirect does not allow it
                        fi.Stream.CopyTo(ms);
                        ms.Position = 0;
                        AddFileToTemplate(template, ms, folderWebRelativeUrl, fileName, folderWebRelativeUrl);
                    }
                }
                catch (WebException exc)
                {
                    WriteWarning($"Can't add file from url {serverRelativeUrl} : {exc}");
                }
            }
            else
            {
                if (!System.IO.Path.IsPathRooted(Source))
                {
                    Source = System.IO.Path.Combine(SessionState.Path.CurrentFileSystemLocation.Path, Source);
                }

                // Load the file and add it to the .PNP file
                using (var fs = System.IO.File.OpenRead(Source))
                {
                    Folder = Folder.Replace("\\", "/");

                    var fileName = Source.IndexOf("\\", StringComparison.Ordinal) > 0 ? Source.Substring(Source.LastIndexOf("\\") + 1) : Source;
                    var container = !string.IsNullOrEmpty(Container) ? Container : string.Empty;
                    AddFileToTemplate(template, fs, Folder, fileName, container);
                }
            }
        }

        private void AddFileToTemplate(ProvisioningTemplate template, Stream fs, string folder, string fileName, string container)
        {
            var source = !string.IsNullOrEmpty(container) ? (container + "/" + fileName) : fileName;

            template.Connector.SaveFileStream(fileName, container, fs);

            if (template.Connector is ICommitableFileConnector)
            {
                ((ICommitableFileConnector)template.Connector).Commit();
            }

            var existing = template.Files.FirstOrDefault(f =>
              f.Src == $"{container}/{fileName}"
              && f.Folder == folder);

            if (existing != null)
                template.Files.Remove(existing);

            var newFile = new OfficeDevPnP.Core.Framework.Provisioning.Model.File
            {
                Src = source,
                Folder = folder,
                Level = FileLevel,
                Overwrite = FileOverwrite,
            };

            template.Files.Add(newFile);

            // Determine the output file name and path
            var outFileName = System.IO.Path.GetFileName(Path);
            var outPath = new FileInfo(Path).DirectoryName;

            var fileSystemConnector = new FileSystemConnector(outPath, "");
            var formatter = XMLPnPSchemaFormatter.LatestFormatter;
            var extension = new FileInfo(Path).Extension.ToLowerInvariant();
            if (extension == ".pnp")
            {
                var provider = new XMLOpenXMLTemplateProvider(template.Connector as OpenXMLConnector);
                var templateFileName = outFileName.Substring(0, outFileName.LastIndexOf(".", StringComparison.Ordinal)) + ".xml";

                provider.SaveAs(template, templateFileName, formatter, TemplateProviderExtensions);
            }
            else
            {
                XMLTemplateProvider provider = new XMLFileSystemTemplateProvider(Path, "");
                provider.SaveAs(template, Path, formatter, TemplateProviderExtensions);
            }
        }
    }
}