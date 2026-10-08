using Cmf.CLI.Core;
using Cmf.CLI.Core.Constants;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using SharpCompress.Archives;
using Spectre.Console;
using System.IO.Abstractions;
using System.Text;
using Color = Spectre.Console.Color;
using Table = Spectre.Console.Table;

namespace audit.Objects
{
    public class Utilities
    {
        public static async Task<(IDirectoryInfo workingDir, CmfPackage cmfPackage, IDirectoryInfo rootDir)> PrepareAzureRepo(IDirectoryInfo workingDir, string azureDevOpsUrl, string teamProject, string repository, string personalAccessToken, string packageId, AzureDevOpsClientWrapper azureDevOpsClient = null)
        {
            #region Prepare Azure Repo

            azureDevOpsClient ??= new AzureDevOpsClientWrapper(azureDevOpsUrl, teamProject, repository, personalAccessToken);

            #region Prepare Workspace to run Audit

            CmfPackage cmfPackage = null;
            var rootDir = workingDir;
            if (rootDir.GetFiles("cmfpackage.json").Count() == 0)
            {
                if (packageId == null)
                {
                    throw new CliException("Please specify a package name");
                }

                rootDir = await Utilities.GetAllCmfPackagesAsync(azureDevOpsClient, repository, workingDir.FileSystem);

                foreach (var element in rootDir.GetFiles("cmfpackage.json", SearchOption.AllDirectories))
                {
                    var package = CmfPackage.Load(element, setDefaultValues: true, workingDir.FileSystem);

                    if (package.PackageId == packageId)
                    {
                        cmfPackage = package;
                        break;
                    }
                }

                if (cmfPackage == null)
                {
                    throw new CliException($"No package found for with ID '{packageId}'");
                }
                workingDir = cmfPackage.GetFileInfo().Directory;
            }
            else
            {
                IFileInfo cmfPackageFile = workingDir.FileSystem.FileInfo.New($"{workingDir.FullName}/cmfpackage.json");
                cmfPackage = CmfPackage.Load(cmfPackageFile, setDefaultValues: true, workingDir.FileSystem);

                workingDir.FileSystem.Directory.SetCurrentDirectory(workingDir.FullName);
                rootDir = FileSystemUtilities.GetProjectRoot(workingDir.FileSystem);
            }
            var remotePath = cmfPackage.GetFileInfo().Directory.FullName.Replace(rootDir.FullName, "").Replace("\\", "/");

            #endregion Prepare Workspace to run Audit

            #region Download the data package

            using (var zipStream = azureDevOpsClient.GetRepositoryItemsAsZip(
                repository,
                path: remotePath
            ))
            {
                if (zipStream == null)
                {
                    throw new CliException("Failed to get zip stream.");
                }

                // Prepare zip for iteration
                using var memoryStream = new MemoryStream();
                await zipStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;  // Reset to the start for reading

                // Open the archive
                using (var archive = ArchiveFactory.Open(memoryStream))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            // Get the full output path
                            string entryOutputPath = SafeCombine(workingDir.Parent.FullName, entry.Key);

                            // Ensure directory exists
                            var dir = workingDir.FileSystem.DirectoryInfo.New(Path.GetDirectoryName(entryOutputPath));
                            dir.Create();

                            // Extract the file
                            using (var entryStream = entry.OpenEntryStream())
                            using (var fileStream = workingDir.FileSystem.File.Create(entryOutputPath))
                            {
                                await entryStream.CopyToAsync(fileStream);
                            }
                        }
                    }
                }
            }

            #endregion Download the data package

            #endregion
            return (workingDir, cmfPackage, rootDir);
        }

        /// <summary>
        /// Combines <paramref name="root"/> with a path that comes from untrusted data (archive entries,
        /// repository item paths), guaranteeing the result stays inside <paramref name="root"/>.
        /// </summary>
        /// <exception cref="CliException">When the resolved path escapes <paramref name="root"/>.</exception>
        public static string SafeCombine(string root, string relativePath)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var rootFullPath = Path.GetFullPath(root);
            var rootWithSeparator = Path.EndsInDirectorySeparator(rootFullPath) ? rootFullPath : rootFullPath + Path.DirectorySeparatorChar;
            var combined = Path.GetFullPath(Path.Combine(rootFullPath, relativePath.TrimStart('/', '\\')));

            if (!combined.StartsWith(rootWithSeparator, comparison) && !string.Equals(combined, rootFullPath, comparison))
            {
                throw new CliException($"Refusing to use path '{relativePath}' as it resolves outside of '{rootFullPath}'.");
            }

            return combined;
        }

        /// <summary>
        /// Will merge all the csvs in a directory and generate an excel file
        /// </summary>
        /// <param name="rootAuditFolder"></param>
        /// <param name="fileName"></param>
        public static void MergeDirectoryCSVsIntoExcel(IDirectoryInfo rootAuditFolder, string fileName = "Merged.xlsx", SearchOption searchOption = SearchOption.TopDirectoryOnly, bool createGroupSheets = false)
        {
            var csvFiles = rootAuditFolder.GetFiles("*.csv", searchOption);
            var sheetNameLookup = new Dictionary<string, string>(); // ShortName -> FullName

            Dictionary<string, List<Row>> groupedSheets = new Dictionary<string, List<Row>>();

            using (SpreadsheetDocument document = SpreadsheetDocument.Create(Path.Combine(rootAuditFolder.FullName, fileName), DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                Sheets sheets = new();
                workbookPart.Workbook.AppendChild(sheets);

                uint sheetId = 1;
                Dictionary<string, string> nameAbbreviation = new Dictionary<string, string>();

                foreach (var file in csvFiles)
                {
                    List<string> lines = file.ReadToStringList();
                    WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    SheetData sheetData = new();
                    List<Row> rowList = [];

                    foreach (var line in lines)
                    {
                        Row row = new Row();
                        var cells = line.Split(',');

                        foreach (var cellValue in cells)
                        {
                            Cell cell;
                            if (Int32.TryParse(cellValue, out int value))
                            {
                                cell = new Cell()
                                {
                                    DataType = CellValues.Number,
                                    CellValue = new CellValue(cellValue)
                                };

                            }
                            else
                            {
                                cell = new Cell()
                                {
                                    DataType = CellValues.String,
                                    CellValue = new CellValue(cellValue)
                                };
                            }
                            row.Append(cell);
                        }
                        rowList.Add(row);
                        sheetData.Append(row);
                    }

                    worksheetPart.Worksheet = new Worksheet(sheetData);

                    string fullSheetName = Path.GetFileNameWithoutExtension(file.Name);

                    // Truncate or remap long sheet names
                    string sheetName = SanitizeSheetName(fullSheetName);
                    if (fullSheetName != sheetName)
                    {
                        sheetNameLookup.Add(sheetName, fullSheetName);
                    }

                    Sheet sheet = new Sheet()
                    {
                        Id = document.WorkbookPart.GetIdOfPart(worksheetPart),
                        SheetId = sheetId++,
                        Name = sheetName
                    };

                    if (createGroupSheets)
                    {
                        var shortName = sheetName.Split("_")[0];
                        nameAbbreviation.TryAdd(shortName, fullSheetName.Split("_")[0]);

                        if (groupedSheets.ContainsKey(shortName))
                        {
                            rowList.RemoveAt(0);
                            groupedSheets[shortName].AddRange(rowList);
                        }
                        else
                        {
                            groupedSheets.Add(shortName, rowList);
                        }
                    }

                    sheets.Append(sheet);
                }

                // Add lookup sheet if any name was shortened
                if (sheetNameLookup.Count > 0)
                {
                    WorksheetPart lookupPart = workbookPart.AddNewPart<WorksheetPart>();
                    SheetData lookupData = new SheetData();

                    // Add headers
                    Row header = new Row();
                    header.Append(CreateCell("Short Name"), CreateCell("Original Name"));
                    lookupData.Append(header);

                    foreach (var kvp in sheetNameLookup)
                    {
                        Row row = new Row();
                        row.Append(CreateCell(kvp.Key), CreateCell(kvp.Value));
                        lookupData.Append(row);
                    }

                    lookupPart.Worksheet = new Worksheet(lookupData);

                    Sheet lookupSheet = new Sheet()
                    {
                        Id = document.WorkbookPart.GetIdOfPart(lookupPart),
                        SheetId = sheetId++,
                        Name = "Sheet Lookup"
                    };

                    sheets.Append(lookupSheet);
                }

                if (createGroupSheets)
                {
                    foreach (KeyValuePair<string, List<Row>> groupSheet in groupedSheets)
                    {
                        WorksheetPart merged = workbookPart.AddNewPart<WorksheetPart>();

                        SheetData sheetData = new SheetData();
                        groupSheet.Value.ForEach(r => sheetData.Append((Row)r.CloneNode(true)));
                        merged.Worksheet = new Worksheet(sheetData);
                        Sheet sheet = new()
                        {
                            Id = document.WorkbookPart.GetIdOfPart(merged),
                            SheetId = sheetId++,
                            Name = nameAbbreviation[groupSheet.Key]
                        };
                        sheets.Append(sheet);
                    }
                }

                workbookPart.Workbook.Save();
            }

            Console.WriteLine($"Excel created at {rootAuditFolder.FullName}");
        }

        private static string SanitizeSheetName(string name)
        {
            // Remove invalid characters
            string clean = new(name.Where(c => !"[]:*?/\\'".Contains(c)).ToArray());
            // Always add the prefix
            clean = (string.Join("", clean.Split("_")[0].Where(c => char.IsUpper(c)).ToArray()) + "_" + clean).Replace(" ", "");
            if (clean.Length > 31)
            {
                // Remove middle part
                string shortName = (clean.Split("_")[0] + "_" + clean.Split("_")[2]);
                if (shortName.Length > 31)
                {
                    // Shorten Package Name
                    shortName = (shortName.Split("_")[0] + "_" + shortName.Split("_")[1].Split(".").Last()).Replace(" ", "");
                }
                // To avoid collisions fill with guid
                clean = (shortName + "_" + Guid.NewGuid().ToString("N")).Substring(0, 31);
            }

            return clean;
        }

        private static Cell CreateCell(string value) => new Cell
        {
            DataType = CellValues.String,
            CellValue = new CellValue(value)
        };

        /// <summary>
        /// Create a Spectre Panel and Generate a CSV file
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="data"></param>
        /// <param name="title"></param>
        /// <param name="borderColor"></param>
        /// <param name="definedColumnNames"></param>
        /// <returns></returns>
        public static Action<IDirectoryInfo, string[]> DisplayPanel(
                string packageId,
                Dictionary<string, string> data,
                string title = "Results",
                Color? borderColor = null,
                string[] definedColumnNames = null)
        {
            return DisplayPanel(packageId, data.ToDictionary(kv => kv.Key, kv => new[] { kv.Value }), title, borderColor, definedColumnNames);
        }

        public static Action<IDirectoryInfo, string[]> DisplayPanel(
                                string packageId,
                                Dictionary<string, string[]> data,
                                string title = "Results",
                                Color? borderColor = null,
                                string[] definedColumnNames = null)
        {
            var panelContent = new StringBuilder();
            foreach (var element in data)
            {
                var values = string.Join(" | ", element.Value);
                panelContent.AppendLine($"{element.Key} -> {values.Replace(",", ";")}");
            }

            var panel = new Panel(new Markup(Markup.Escape(panelContent.ToString())))
            {
                Header = new PanelHeader($"{packageId}: {title}"),
                Border = BoxBorder.Double,
                Expand = true
            };

            if (borderColor != null)
            {
                panel.BorderColor((Color)borderColor);
            }

            AnsiConsole.Write(panel);

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[blue]────────────────────────────────────────[/]").Centered());
            AnsiConsole.WriteLine();

            return (IDirectoryInfo rootAuditPath, string[] columnNames) => Utilities.GenerateCsv(data, Path.Join(rootAuditPath.FullName, $"{title}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.csv"), columnNames ?? definedColumnNames);
        }

        /// <summary>
        /// Create a Spectre Table and Generate a CSV file
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="data"></param>
        /// <param name="definedColumnNames"></param>
        /// <param name="title"></param>
        /// <param name="borderColor"></param>
        /// <returns></returns>
        public static Action<IDirectoryInfo> DisplayTable(
                string packageId,
                Dictionary<string, string> data,
                string[] definedColumnNames,
                string title = "Results",
                Color? borderColor = null)
        {
            List<List<string>> result = data
                .Select(kvp => new List<string> { kvp.Key, kvp.Value })
                .ToList();

            return Utilities.DisplayTable(packageId, result, definedColumnNames, title, borderColor);
        }

        /// <summary>
        /// Create a Spectre Table and Generate a CSV file
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="data"></param>
        /// <param name="definedColumnNames"></param>
        /// <param name="title"></param>
        /// <param name="borderColor"></param>
        /// <returns></returns>
        public static Action<IDirectoryInfo> DisplayTable(
                        string packageId,
                        Dictionary<string, string[]> data,
                        string[] definedColumnNames,
                        string title = "Results",
                        Color? borderColor = null)
        {
            List<List<string>> result = data
                .Select(kvp => new List<string> { kvp.Key }.Concat(kvp.Value).ToList())
                .ToList();

            return Utilities.DisplayTable(packageId, result, definedColumnNames, title, borderColor);
        }

        /// <summary>
        /// Create a Spectre Table and Generate a CSV file
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="data"></param>
        /// <param name="definedColumnNames"></param>
        /// <param name="title"></param>
        /// <param name="borderColor"></param>
        /// <returns></returns>
        public static Action<IDirectoryInfo> DisplayTable(
                string packageId,
                List<List<string>> data,
                string[] definedColumnNames,
                string title = "Results",
                Color? borderColor = null)
        {
            var table = new Table
            {
                Title = new TableTitle($"{packageId}: {title}")
            };

            foreach (var columnName in definedColumnNames)
            {
                table.AddColumn(columnName);
            }

            if (borderColor != null)
            {
                table.BorderColor((Color)borderColor);
            }

            foreach (var element in data)
            {
                table.AddRow(element.Select(el => el == null ? el = "" : (el.Replace("[", "|").Replace("]", "|"))).ToArray());
            }

            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[blue]────────────────────────────────────────[/]").Centered());
            AnsiConsole.WriteLine();

            return (IDirectoryInfo rootAuditPath) => Utilities.GenerateCsv(data, Path.Join(rootAuditPath.FullName, $"{title}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.csv"), definedColumnNames);
        }

        /// <summary>
        /// Generate a CSV file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="csvPath"></param>
        /// <param name="columnNames"></param>
        public static void GenerateCsv(
            Dictionary<string, string[]> data,
            string csvPath,
            string[] columnNames)
        {
            List<List<string>> result = data
                .Select(kvp => new List<string> { kvp.Key }.Concat(kvp.Value).ToList())
                .ToList();

            GenerateCsv(result, csvPath, columnNames);
        }

        /// <summary>
        /// Generate a CSV file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="csvPath"></param>
        /// <param name="columnNames"></param>
        public static void GenerateCsv(
            List<List<string>> data,
            string csvPath,
            string[] columnNames)
        {
            var csvContent = new StringBuilder();

            // Add CSV headers
            csvContent.AppendLine(string.Join(",", columnNames));

            // Add data rows
            foreach (var element in data)
            {
                while (columnNames.Length > element.Count)
                {
                    element.Add("");
                }
                csvContent.AppendLine(string.Join(",", element.Select(EscapeCsvValue)));
            }

            File.WriteAllText(csvPath, csvContent.ToString());

            AnsiConsole.MarkupLine($"[green]Report file generated at:[/] [underline]{csvPath}[/]");
        }

        /// <summary>
        /// Remove values added for Spectre logging
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string EscapeCsvValue(string value)
        {
            //return value.Trim().Replace("->", ",").Replace("|", ",");
            return value.Trim().Replace(",", ";").Replace("|", ";").Replace("[", "").Replace("]", "").Replace("/", "").Replace("-", "");
        }

        #region Azure devops

        /// <summary>
        /// Get all Cmf Packages from azure devops
        /// </summary>
        /// <param name="azureDevOpsClient"></param>
        /// <param name="repository"></param>
        /// <param name="fileSystem"></param>
        /// <returns></returns>
        public static async Task<IDirectoryInfo> GetAllCmfPackagesAsync(AzureDevOpsClientWrapper azureDevOpsClient, string repository, IFileSystem fileSystem)
        {
            var items = azureDevOpsClient.GetRepositoryItems(repository, "/");

            // Filter only cmfpackage.json and .project-config.json project files
            var cmfPackageFiles = items.Where(item => item.Path.EndsWith("cmfpackage.json", StringComparison.OrdinalIgnoreCase) || item.Path.EndsWith(".project-config.json", StringComparison.OrdinalIgnoreCase));
            var workingDirectory = fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            workingDirectory.Create();

            // Parallel processing with async support
            Parallel.ForEachAsync(
                cmfPackageFiles,
                new ParallelOptions { MaxDegreeOfParallelism = 4 }, // Adjust based on your needs
                async (item, cancellationToken) =>
                {
                    try
                    {
                        // Download the file content asynchronously
                        await using var fileContent = azureDevOpsClient.GetRepositoryItemContent(repository, item.Path);

                        // Create directory structure locally
                        var fileInfo = fileSystem.FileInfo.New(SafeCombine(workingDirectory.FullName, item.Path));
                        fileInfo.Directory.Create();

                        await using var fileStream = fileInfo.Open(FileMode.Create, FileAccess.Write);
                        await fileContent.CopyToAsync(fileStream);
                    }
                    catch (Exception ex)
                    {
                        // Log or handle the error
                        Log.Error($"Error processing {item.Path}: {ex.Message}");
                    }
                }).GetAwaiter().GetResult();

            return workingDirectory;
        }

        /// <summary>
        /// Get content of a cmf package json from azure devops
        /// </summary>
        /// <param name="azureDevOpsClient"></param>
        /// <param name="repository"></param>
        /// <param name="cmfpackagejson"></param>
        /// <returns></returns>
        public static async Task<(string, CmfPackage)> GetCmfPackageJsonContentAsync(AzureDevOpsClientWrapper azureDevOpsClient, string repository, string cmfpackagejson = "/cmfpackage.json")
        {
            var content = azureDevOpsClient.GetRepositoryItemContent(repository, cmfpackagejson);
            var cmfpackage = await StreamToStringAsync(content);

            return (cmfpackage, JsonConvert.DeserializeObject<CmfPackage>(cmfpackage));
        }

        /// <summary>
        /// Get content of a file from azure devops
        /// </summary>
        /// <param name="azureDevOpsClient"></param>
        /// <param name="repository"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task<string> GetFileContentAsync(AzureDevOpsClientWrapper azureDevOpsClient, string repository, string fileName)
        {
            var content = azureDevOpsClient.GetRepositoryItemContent(repository, fileName);
            if (content != null)
            {
                return await StreamToStringAsync(content);
            }
            return null;
        }

        /// <summary>
        /// Get JSON content of a file from azure devops
        /// </summary>
        /// <param name="azureDevOpsClient"></param>
        /// <param name="repository"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task<(string, Dictionary<string, object>)> GetJsonFileContentAsync(AzureDevOpsClientWrapper azureDevOpsClient, string repository, string fileName)
        {
            var content = azureDevOpsClient.GetRepositoryItemContent(repository, fileName);

            if (content != null)
            {
                var projectContent = await StreamToStringAsync(content);

                return (projectContent, System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(projectContent));
            }
            return (null, null);
        }

        /// <summary>
        /// Get project config content from azure devops
        /// </summary>
        /// <param name="azureDevOpsClient"></param>
        /// <param name="repository"></param>
        /// <param name="projectConfigFileName"></param>
        /// <returns></returns>
        public static async Task<(string, ProjectConfig)> GetProjectConfigContentAsync(
            AzureDevOpsClientWrapper azureDevOpsClient,
            string repository,
            string projectConfigFileName = CoreConstants.ProjectConfigFileName)
        {
            var content = azureDevOpsClient.GetRepositoryItemContent(repository, projectConfigFileName);
            var projectContent = await StreamToStringAsync(content);

            var config = JsonConvert.DeserializeObject<ProjectConfig>(projectContent);

            return (projectContent, config);
        }

        /// <summary>
        /// Get content of a file already present on local disk, relative to <paramref name="rootDir"/>.
        /// Mirrors <see cref="GetFileContentAsync"/>'s null-if-missing contract for a local checkout.
        /// </summary>
        /// <param name="rootDir"></param>
        /// <param name="fileSystem"></param>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public static async Task<string> GetLocalFileContentAsync(IDirectoryInfo rootDir, IFileSystem fileSystem, string relativePath)
        {
            while (rootDir != null && !fileSystem.File.Exists(fileSystem.Path.Join(rootDir.FullName, relativePath.TrimStart('/'))))
            {
                rootDir = rootDir.Parent;
            }

            if (rootDir == null)
            {
                return null;
            }

            var filePath = Path.Combine(rootDir.FullName, relativePath.TrimStart('/'));
            var file = fileSystem.FileInfo.New(filePath);

            if (!file.Exists)
            {
                return null;
            }

            using var stream = file.OpenRead();
            return await StreamToStringAsync(stream);
        }

        /// <summary>
        /// Convert stream to string
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<string> StreamToStringAsync(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // Ensure the stream is at the beginning
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            // Use StreamReader to read the stream into a string
            using (var streamReader = new StreamReader(stream))
            {
                return await streamReader.ReadToEndAsync();
            }
        }

        #endregion Azure devops
    }
}