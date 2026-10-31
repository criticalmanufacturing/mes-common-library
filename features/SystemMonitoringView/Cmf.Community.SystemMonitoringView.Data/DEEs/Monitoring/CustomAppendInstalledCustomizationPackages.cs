using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.Common.Logging;
using Cmf.Foundation.Configuration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Cmf.Community.SystemMonitoringView.Actions.Monitoring
{
    public class CustomAppendInstalledCustomizationPackages : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info
            /// <summary>
            /// Summary: Appends the installed customization packages (view UserDataModel.V_InstalledPackage) that match the
            /// configured SQL LIKE patterns to the monitoring information shown in Application > Versioning.
            /// A blank or missing pattern value shows nothing (and does not query the view): set the patterns to show packages.
            /// The default value lives in the master data that creates the config entry.
            /// Action Groups: ApplicationMonitoringManagement.ApplicationMonitoringManagementOrchestration.GetApplicationMonitoringInformation.Post
            /// </summary>
            #endregion Info

            // The request must be for all contexts (null/empty/missing) or include the customization versioning context.
            // Orchestration-level hook: the service Input object is found by type name (key names unknown, orchestration types are not
            // resolvable by the runtime DEE compiler, hence reflection).
            object request = Input.Values.FirstOrDefault(v => v != null && v.GetType().Name == "GetApplicationMonitoringInformationInput");
            IEnumerable<MonitoringContext> contexts = request?.GetType().GetProperty("MonitoringContexts")?.GetValue(request) as IEnumerable<MonitoringContext>;

            return contexts == null || !contexts.Any() || contexts.Contains(MonitoringContext.CustomizationVersioning);

            //---End DEE Condition Code---
        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            UseReference("Cmf.Foundation.BusinessObjects.dll", "Cmf.Foundation.BusinessObjects");
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Logging");
            UseReference("Cmf.Foundation.Configuration.dll", "Cmf.Foundation.Configuration.Abstractions");
            UseReference("", "System.Data");
            UseReference("", "System.Globalization");
            UseReference("", "System.Linq");
            
            #region Constants
            const string OutputTypeName = "GetApplicationMonitoringInformationOutput";
            const string OutputProperty = "MonitoringInformation";
            const string InstalledPackagesReaderKey = "InstalledPackagesReader";   // optional Func<DataTable>: test seam, replaces the view query
            const string PackagePatternsReaderKey = "PackagePatternsReader";       // optional Func<string>: test seam, replaces the config read
            const string PatternsConfigPath = "/Community/SystemMonitoringView/InstalledPackagePatterns/";
            const string InstalledPackagesQuery = "SELECT PackageId, PackageVersion, InstalledOn, COUNT(*) OVER() AS TotalRows FROM UserDataModel.V_InstalledPackage";
            const string PackageIdColumn = "PackageId";
            const string PackageVersionColumn = "PackageVersion";
            const string InstalledOnColumn = "InstalledOn";
            const string InstalledOnFormat = "yyyy-MM-dd HH:mm:ss";
            #endregion Constants

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];

            try
            {
                object output = Input.Values.FirstOrDefault(v => v != null && v.GetType().Name == OutputTypeName);
                if (output == null)
                {
                    // list key names and value type names only, never values
                    string present = string.Join(", ", Input.Select(kv => kv.Key + " (" + (kv.Value == null ? "null" : kv.Value.GetType().FullName) + ")"));
                    LogWarning("no " + OutputTypeName + " object found, no customization packages added. Input provides: " + present, null);
                    return Input;
                }

                MonitoringInformationCollection rows = output.GetType().GetProperty(OutputProperty)?.GetValue(output) as MonitoringInformationCollection;
                if (rows == null)
                {
                    LogWarning(OutputTypeName + "." + OutputProperty + " is null, no customization packages added.", null);
                    return Input;
                }

                List<string> patterns = (ReadPatterns() ?? "")
                    .Split(';')
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToList();
                if (patterns.Count == 0)
                {
                    // blank or missing entry: show nothing, by design
                    return Input;
                }

                // the view returns one row per package id (latest installed version), so no de-duplication here
                var packages = new List<(string Id, string Version, DateTime InstalledOn)>();
                foreach (DataRow row in ReadInstalledPackages().Rows)
                {
                    string id = Convert.ToString(row[PackageIdColumn], CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(id) || !patterns.Any(p => LikeMatch(id, p)))
                    {
                        continue;
                    }

                    packages.Add((id, Convert.ToString(row[PackageVersionColumn], CultureInfo.InvariantCulture), row[InstalledOnColumn] is DateTime d ? d : DateTime.MinValue));
                }

                foreach (var package in packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
                {
                    rows.Add(new MonitoringInformation(MonitoringContext.CustomizationVersioning, MonitoringPath.Application, MonitoringPath.Versioning)
                    {
                        Name = package.Id,
                        Value = package.Version,
                        Text = package.InstalledOn.ToString(InstalledOnFormat, CultureInfo.InvariantCulture),
                        Status = MonitoringStatus.OK
                    });
                }
            }
            catch (Exception ex)
            {
                // never break the monitoring page: product rows stay untouched
                LogWarning("could not add the installed customization packages.", ex);
            }

            string ReadPatterns()
            {
                string configured = null;
                if (Input.TryGetValue(PackagePatternsReaderKey, out object readerObject) && readerObject is Func<string> reader)
                {
                    configured = reader();
                }
                else
                {
                    IConfig config = serviceProvider.GetService<IConfig>();
                    if (config != null && config.TryLoad(PatternsConfigPath))
                    {
                        configured = config.Value as string;
                    }
                }

                return configured;
            }

            DataTable ReadInstalledPackages()
            {
                if (Input.TryGetValue(InstalledPackagesReaderKey, out object readerObject) && readerObject is Func<DataTable> reader)
                {
                    return reader();
                }

                // fixed statement: configured patterns are applied in C#, never concatenated into SQL.
                // FromQuery reads and removes a TotalRows column from the result, hence the COUNT(*) OVER() in the statement.
                DataSet data = NgpDataSet.ToDataSet(NgpDataSet.FromQuery(out int _, InstalledPackagesQuery));
                return data.Tables[0];
            }

            // SQL LIKE, anchored and case-insensitive: % = any run of characters (also empty), _ = exactly one character, everything else literal.
            // shortcut: [] character sets and escaping are not supported, _ and % always act as wildcards, upgrade when a pattern needs a literal _ or % or a character set.
            // Hand written because the runtime DEE compiler does not reference System.Text.RegularExpressions.
            bool LikeMatch(string text, string pattern)
            {
                int t = 0, p = 0, starP = -1, starT = 0;
                while (t < text.Length)
                {
                    if (p < pattern.Length && pattern[p] == '%')
                    {
                        starP = p++;
                        starT = t;
                    }
                    else if (p < pattern.Length && (pattern[p] == '_' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t])))
                    {
                        p++;
                        t++;
                    }
                    else if (starP >= 0)
                    {
                        p = starP + 1;
                        t = ++starT;
                    }
                    else
                    {
                        return false;
                    }
                }

                while (p < pattern.Length && pattern[p] == '%')
                {
                    p++;
                }

                return p == pattern.Length;
            }

            void LogWarning(string message, Exception ex)
            {
                // also stderr of the host process: the Cmf logging service may not reach the host log
                Console.Error.WriteLine("CustomAppendInstalledCustomizationPackages: " + message + (ex == null ? "" : " " + ex.Message));
                try
                {
                    // ILoggingService only (Cmf ILogger derives from a Microsoft.Extensions.Logging type the DEE compiler does not reference)
                    ILoggingService logging = serviceProvider?.GetService<ILoggingService>();
                    logging?.Warning("CustomAppendInstalledCustomizationPackages: " + message + (ex == null ? "" : " " + ex.GetType().Name + ": " + ex.Message), ex);
                }
                catch (Exception)
                {
                    // logging must never break the monitoring page
                }
            }

            //---End DEE Code---
            return Input;
        }
    }
}
