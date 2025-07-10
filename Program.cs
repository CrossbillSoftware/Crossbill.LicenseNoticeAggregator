using HtmlAgilityPack;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Crossbill.LicenseNoticeAggregator
{
    internal class Program
    {
        private const string _nugetDir = "c:\\Users\\Pavlushka\\.nuget\\packages";

        static void Main(string[] args)
        {
            string project = null;
            string output = null;
            string extra = null;
            string exclude = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != null && args[i] == "/project")
                {
                    project = args[i + 1];
                    while (project.StartsWith("'") && !project.EndsWith("'"))
                    {
                        i++;
                        project += " " + args[i + 1];
                    }
                    if (project.StartsWith("'"))
                    {
                        project = project.Substring(1, project.Length - 2);
                    }
                }
                else if (args[i] != null && args[i] == "/output")
                {
                    output = args[i + 1];
                    while (output.StartsWith("'") && !output.EndsWith("'"))
                    {
                        i++;
                        output += " " + args[i + 1];
                    }
                    if (output.StartsWith("'"))
                    {
                        output = output.Substring(1, output.Length - 2);
                    }
                }
                else if (args[i] != null && args[i] == "/extra")
                {
                    extra = args[i + 1];
                    while (extra.StartsWith("'") && !extra.EndsWith("'"))
                    {
                        i++;
                        extra += " " + args[i + 1];
                    }
                    if (extra.StartsWith("'"))
                    {
                        extra = extra.Substring(1, extra.Length - 2);
                    }
                }
                else if (args[i] != null && args[i] == "/exclude")
                {
                    exclude = args[i + 1];
                    while (exclude.StartsWith("'") && !exclude.EndsWith("'"))
                    {
                        i++;
                        exclude += " " + args[i + 1];
                    }
                    if (exclude.StartsWith("'"))
                    {
                        exclude = exclude.Substring(1, exclude.Length - 2);
                    }
                }
                //if (args[i] != null && args[i] == "/mode")
                //{
                //    mode = args[i + 1];
                //}
            }

            List<string> processedPacks = new List<string>();
            List<string> processedPojects = new List<string>();
            StringBuilder log = new StringBuilder();

            string currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string manualDir = Path.Combine(currentDir, "ManualCheck");
            string extraDir = extra ?? Path.Combine(currentDir, "ExtraLicenses");

            try
            {
                if (String.IsNullOrEmpty(project))
                {
                    throw new Exception("Parameter /project must be set to .csproj file location.");
                }

                string projectName = Path.GetFileNameWithoutExtension(project);
                string targetDir = Path.Combine(currentDir, projectName);
                if (Directory.Exists(targetDir))
                {
                    Delete(targetDir);
                }

                Directory.CreateDirectory(targetDir);
                project = Path.GetFullPath(project);
                ProcessProject(log, targetDir, manualDir, processedPacks, processedPojects, project);

                if (Directory.Exists(extraDir)) 
                {
                    string[] extraFiles = Directory.GetFiles(extraDir);
                    foreach (string extraFile in extraFiles)
                    {
                        string targetExtraFile = Path.Combine(targetDir, Path.GetFileName(extraFile));
                        File.Copy(extraFile, targetExtraFile, true);
                    }
                }

                if (exclude != null && Directory.Exists(exclude))
                {
                    string[] excludeFiles = Directory.GetFiles(exclude);
                    foreach (string excludeFile in excludeFiles)
                    {
                        string targetExcludeFile = Path.Combine(targetDir, Path.GetFileName(excludeFile));
                        if (File.Exists(targetExcludeFile))
                        {
                            File.Delete(targetExcludeFile);
                        }
                    }
                }

                StringBuilder sb = new StringBuilder();
                var files = Directory.GetFiles(targetDir)
                                    .ToLookup(f => (new FileInfo(f)).Length, f => f)
                                    .OrderBy(p => p.Key);
                foreach (var group in files)
                {
                    string firstName = group.First();
                    string name = String.Join(", ", group
                                                    .Select(f => Path.GetFileNameWithoutExtension(f))
                                                    .ToArray());
                    string line = String.Format(Path.GetFileNameWithoutExtension(firstName).Contains("third-party-notices") ? "Third party notices for {0}" : "License notice for {0}", name.Replace("_third-party-notices",""));
                    int len = Math.Min(line.Length, 140);
                    sb.AppendLine("".PadRight(len, '='));
                    sb.AppendLine(line);
                    sb.AppendLine("".PadRight(len, '='));
                    sb.AppendLine();
                    sb.Append(File.ReadAllText(firstName, Encoding.UTF8));
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine();
                }

                string targetFile = output ?? Path.Combine(currentDir, String.Format("{0}.txt", projectName));
                File.WriteAllText(targetFile, sb.ToString(), Encoding.UTF8);

                log.AppendLine("[Info ] Ready");
            }
            catch (Exception ex)
            {
                Exception inner = ex;
                while (inner != null)
                {
                    log.AppendFormat(" {0} {1}", inner.Message, inner.StackTrace);
                    log.AppendLine();
                    inner = inner.InnerException;
                }
            }
            finally
            {
                File.WriteAllText(Path.Combine(currentDir, "licenses.log"), log.ToString());
            }
        }

        private static void ProcessProject(StringBuilder log, string targetDir, string manualDir, List<string> processedPacks, List<string> processedPojects, string project)
        {
            if (processedPojects.Contains(project))
            {
                return;
            }

            log.AppendFormat("[Info ]  Processing project {0}", project);
            log.AppendLine();
            processedPojects.Add(project);

            Dictionary<string, string> unprocessedPacks = new Dictionary<string, string>();

            XDocument doc = XDocument.Load(project);
            var refNodes = doc.XPathSelectElements("//*[local-name() = 'PackageReference']").ToList();
            if (refNodes != null && refNodes.Count > 0)
            {
                foreach (var refNode in refNodes)
                {
                    string name = refNode.Attribute("Include").Value;
                    string version = refNode.Attribute("Version").Value;
                    if (!unprocessedPacks.ContainsKey(name))
                    {
                        unprocessedPacks.Add(name, version);
                    }
                }
            }

            string platform = "net8.0";
            var frameworkNode = doc.XPathSelectElement("//*[local-name() = 'TargetFramework']");
            if (frameworkNode != null)
            {
                platform = frameworkNode.Value;
                if (platform.Contains(','))
                {
                    platform = platform.Split(',')[0];
                }
            }

            ProcessNugetPackage(log, targetDir, manualDir, processedPacks, unprocessedPacks, platform, project);

            log.AppendFormat("[Info ]  Done project {0}", project);
            log.AppendLine();

            var projNodes = doc.XPathSelectElements("//*[local-name() = 'ProjectReference']").ToList();
            if (projNodes != null && projNodes.Count > 0)
            {
                foreach (var projNode in projNodes)
                {
                    string name = projNode.Attribute("Include").Value;
                    string projPath = Path.Combine(Path.GetDirectoryName(project), name);
                    projPath = Path.GetFullPath(projPath);
                    if (File.Exists(projPath))
                    {
                        ProcessProject(log, targetDir, manualDir, processedPacks, processedPojects, projPath);
                    }                    
                }
            }

            var projRefNodes = doc.XPathSelectElements("//*[local-name() = 'Reference']/*[local-name() = 'HintPath']").ToList();
            if (projRefNodes != null && projRefNodes.Count > 0)
            {
                foreach (var projRefNode in projRefNodes)
                {
                    string name = projRefNode.Value;
                    name = name.Replace("\\bin\\$(Configuration)\\$(TargetFramework)", "");
                    string projPath = Path.Combine(Path.GetDirectoryName(project), name);
                    projPath = Path.ChangeExtension(projPath, ".csproj");
                    projPath = Path.GetFullPath(projPath);
                    if (File.Exists(projPath))
                    {
                        ProcessProject(log, targetDir, manualDir, processedPacks, processedPojects, projPath);
                    }
                }
            }
        }

        private static void ProcessNugetPackage(StringBuilder log, string targetDir, string manualDir, List<string> processedPacks, Dictionary<string, string> unprocessedPacks, string platform, string caller)
        {
            foreach (var pair in unprocessedPacks)
            {
                string pack = String.Format("{0}/{1}", pair.Key, pair.Value);
                if (processedPacks.Contains(pack))
                {
                    continue;
                }

                processedPacks.Add(pack);

                if (String.IsNullOrEmpty(pair.Value))
                {
                    log.AppendFormat("[Error] Version not found for {0}. Referenced in {1}", pack, caller);
                    log.AppendLine();
                    continue;
                }

                string packDir = Path.Combine(_nugetDir, pair.Key, pair.Value);
                string nuspec = Path.Combine(packDir, String.Format("{0}.nuspec", pair.Key));
                XDocument doc = XDocument.Load(nuspec);

                string licenseText = null;
                string licFile = Path.Combine(packDir, "LICENSE");
                if (manualDir != null)
                {
                    string manualFile = Path.Combine(manualDir, String.Format("{0}.txt", pair.Key));
                    if (File.Exists(manualFile))
                    {
                        licFile = manualFile;

                        log.AppendFormat("[Info ]    Manual license used for {0}", pair.Key);
                        log.AppendLine();
                    }
                }

                if (!File.Exists(licFile))
                {
                    licFile = Path.Combine(packDir, "LICENSE.txt");
                    if (!File.Exists(licFile))
                    {
                        licFile = Path.Combine(packDir, "LICENSE.md");
                        if (!File.Exists(licFile))
                        {
                            licFile = Path.Combine(packDir, "dotnet_library_license.txt");
                            if (!File.Exists(licFile))
                            {
                                licFile = null;
                            }
                        }
                    }
                }

                string copyright = null;
                var copyNode = doc.XPathSelectElement("//*[local-name() = 'copyright']");
                if (copyNode != null)
                {
                    copyright = copyNode.Value;
                }

                string author = null;
                var authorNode = doc.XPathSelectElement("//*[local-name() = 'authors']");
                if (authorNode == null)
                {
                    authorNode = doc.XPathSelectElement("//*[local-name() = 'owners']");
                }

                if (authorNode != null)
                {
                    author = authorNode.Value;
                }

                if (licFile == null)
                {
                    DateTime dt = DateTime.Now;
                    var licNode = doc.XPathSelectElement("//*[local-name() = 'license']");
                    if (licNode != null && !String.IsNullOrEmpty(licNode.Value))
                    {
                        licFile = Path.Combine(packDir, licNode.Value);
                        if (!File.Exists(licFile))
                        {
                            switch (licNode.Value)
                            {
                                case "MIT":
                                    licenseText = String.Format("MIT License\r\n\r\nCopyright (c) {0}\r\n\r\nPermission is hereby granted, free of charge, to any person obtaining a copy\r\nof this software and associated documentation files (the \"Software\"), to deal\r\nin the Software without restriction, including without limitation the rights\r\nto use, copy, modify, merge, publish, distribute, sublicense, and/or sell\r\ncopies of the Software, and to permit persons to whom the Software is\r\nfurnished to do so, subject to the following conditions:\r\n\r\nThe above copyright notice and this permission notice shall be included in all\r\ncopies or substantial portions of the Software.\r\n\r\nTHE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\r\nIMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\r\nFITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\r\nAUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\r\nLIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\r\nOUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\r\nSOFTWARE.", (copyright ?? author ?? "").TrimStart('©'));
                                    break;
                                case "Apache-2.0":
                                    licenseText = String.Format("Copyright {0}\r\n\r\nLicensed under the Apache License, Version 2.0 (the \"License\");\r\nyou may not use this file except in compliance with the License.\r\nYou may obtain a copy of the License at\r\n\r\n   http://www.apache.org/licenses/LICENSE-2.0\r\n\r\nUnless required by applicable law or agreed to in writing, software\r\ndistributed under the License is distributed on an \"AS IS\" BASIS,\r\nWITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.\r\nSee the License for the specific language governing permissions and\r\nlimitations under the License.\r\n", copyright ?? author ?? "");
                                    break;
                                case "BSD-3-Clause":
                                    licenseText = String.Format("Copyright (c) {0}.\r\n\r\nRedistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:\r\n\r\n1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.\r\n2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.\r\n3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.\r\nTHIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS \"AS IS\" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.\r\n", (copyright ?? author ?? "").TrimStart('©'));
                                    break;
                                case "PostgreSQL":
                                    licenseText = String.Format("PostgreSQL Database Management System\r\n(formerly known as Postgres, then as Postgres95)\r\n\r\n{0}\r\n\r\nPortions Copyright (c) 1996-2010, The PostgreSQL Global Development Group\r\n\r\nPortions Copyright (c) 1994, The Regents of the University of California\r\n\r\nPermission to use, copy, modify, and distribute this software and its documentation for any purpose, without fee, and without a written agreement is hereby granted, provided that the above copyright notice and this paragraph and the following two paragraphs appear in all copies.\r\n\r\nIN NO EVENT SHALL THE UNIVERSITY OF CALIFORNIA BE LIABLE TO ANY PARTY FOR DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, INCLUDING LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION, EVEN IF THE UNIVERSITY OF CALIFORNIA HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.\r\n\r\nTHE UNIVERSITY OF CALIFORNIA SPECIFICALLY DISCLAIMS ANY WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS ON AN \"AS IS\" BASIS, AND THE UNIVERSITY OF CALIFORNIA HAS NO OBLIGATIONS TO PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.", copyright ?? author ?? "");
                                    break;
                                default:
                                    log.AppendFormat("[Error] License {1} not implemented, check {0}", pack, licNode.Value);
                                    log.AppendLine();
                                    break;
                            }
                            log.AppendFormat("[Warn ]    License autogenerated, check {0}", pack);
                            log.AppendLine();
                            licFile = null;
                        }
                    }
                }

                string targetLicFile = Path.Combine(targetDir, String.Format("{0}.txt", pair.Key));
                if (licFile != null)
                {
                    string content;
                    if (Path.GetExtension(licFile).ToLower() == ".rtf")
                    {
                        content = RichTextStripper.StripRichTextFormat(File.ReadAllText(licFile));
                    }
                    else
                    {
                        content = File.ReadAllText(licFile);
                    }
                    File.WriteAllText(targetLicFile, content.Trim(' ', '\r', '\n').Replace("\r\n","\n"));
                }
                else if(licenseText != null)
                {
                    File.WriteAllText(targetLicFile, licenseText.Trim(' ', '\r', '\n').Replace("\r\n", "\n"));
                }
                else
                {
                    var licNode = doc.XPathSelectElement("//*[local-name() = 'licenseUrl']");
                    if (licNode != null && !String.IsNullOrEmpty(licNode.Value))
                    {
                        string url = licNode.Value;
                        try
                        {
                            MemoryStream license = Downloader.DownloadLicense(url, false);
                            HtmlDocument hDoc = new HtmlDocument();
                            hDoc.Load(license);
                            File.WriteAllText(targetLicFile, hDoc.DocumentNode.InnerText.Trim(' ', '\r', '\n').Replace("\r\n", "\n"));
                        }
                        catch(Exception ex)
                        {
                            log.AppendFormat("[Warn ]    Error in {0}. Failed to download license from {1}", pack, url);
                            log.AppendLine();
                            targetLicFile = Path.Combine(targetDir, String.Format("_err_{0}.txt", pair.Key));
                            File.WriteAllText(targetLicFile, ex.Message);
                        }
                    }
                }

                string tpFile = Path.Combine(packDir, "THIRD-PARTY-NOTICES");
                if (manualDir != null)
                {
                    string manualFile = Path.Combine(manualDir, String.Format("{0}_third-party-notices.txt", pair.Key));
                    if (File.Exists(manualFile))
                    {
                        tpFile = manualFile;

                        log.AppendFormat("[Info ]    Manual third party notice used for {0}", pack);
                        log.AppendLine();
                    }
                }

                if (!File.Exists(tpFile))
                {
                    tpFile = Path.Combine(packDir, "THIRD-PARTY-NOTICES.TXT");
                    if (!File.Exists(tpFile))
                    {
                        tpFile = Path.Combine(packDir, "THIRD-PARTY-NOTICES.md");
                        if (!File.Exists(tpFile))
                        {
                            tpFile = Path.Combine(packDir, "THIRDPARTYNOTICES.txt");
                            if (!File.Exists(tpFile))
                            {
                                tpFile = Path.Combine(packDir, "THIRDPARTYNOTICES.rtf");
                                if (!File.Exists(tpFile))
                                {
                                    tpFile = null;
                                }
                            }
                        }
                    }
                }

                if (tpFile != null)
                {
                    string targetTpFile = Path.Combine(targetDir, String.Format("{0}_third-party-notices.txt", pair.Key));
                    string content;
                    if (Path.GetExtension(tpFile).ToLower() == ".rtf")
                    {
                        content = RichTextStripper.StripRichTextFormat(File.ReadAllText(tpFile));
                    }
                    else
                    {
                        content = File.ReadAllText(tpFile);
                    }
                    File.WriteAllText(targetTpFile, content.Trim(' ', '\r', '\n').Replace("\r\n", "\n"));
                }

                List<XElement> groupNodes = doc.XPathSelectElements(String.Format("//*[local-name() = 'dependencies']/*[local-name() = 'group'][@targetFramework='{0}']", platform)).ToList();
                if (groupNodes == null || groupNodes.Count == 0)
                {
                    if (platform.StartsWith("net"))
                    {
                        groupNodes = doc.XPathSelectElements("//*[local-name() = 'dependencies']/*[local-name() = 'group'][starts-with(@targetFramework,'net') and not(contains(@targetFramework, '-'))]").ToList();
                    }

                    if (groupNodes == null || groupNodes.Count == 0)
                    {
                        groupNodes = doc.XPathSelectElements("//*[local-name() = 'dependencies']/*[local-name() = 'group'][starts-with(@targetFramework,'.NETCoreApp') and not(contains(@targetFramework, '-'))]").ToList();
                    }

                    if (groupNodes == null || groupNodes.Count == 0)
                    {
                        groupNodes = doc.XPathSelectElements("//*[local-name() = 'dependencies']/*[local-name() = 'group'][starts-with(@targetFramework,'.NETStandard') and not(contains(@targetFramework, '-'))]").ToList();
                    }

                    if (groupNodes != null && groupNodes.Count > 0)
                    {
                        var first = groupNodes
                                        .OrderByDescending(n => n.Attribute("targetFramework").Value)
                                        .First();
                        groupNodes = new List<XElement>() { first };
                    }
                }

                if (groupNodes == null || groupNodes.Count == 0)
                {
                    groupNodes = doc.XPathSelectElements("//*[local-name() = 'dependencies']").ToList();
                }

                if (groupNodes != null && groupNodes.Count > 0)
                {
                    var depNodes = groupNodes.SelectMany(g => g.Elements().Where(e => e.Name.LocalName == "dependency")).ToList();
                    if (depNodes != null && depNodes.Count > 0)
                    {
                        var unprocessedDependencies = depNodes
                                                        //.Where(s => s.Attribute("exclude") == null || (!s.Attribute("exclude").Value.Contains("All") && !s.Attribute("exclude").Value.Contains("Build")))
                                                        .Select(s => new KeyValuePair<string, string>(s.Attribute("id").Value, ProcessVersion(s.Attribute("id").Value, s.Attribute("version").Value)))
                                                        .ToDictionary(s => s.Key, s => s.Value);
                        log.AppendFormat("[Debug]  Processing package {0} dependencies {1}", pack, String.Join(',', unprocessedDependencies.Select(s => String.Format("{0}/{1}", s.Key, s.Value)).ToArray()));
                        log.AppendLine();
                        if (unprocessedDependencies != null && unprocessedDependencies.Count > 0)
                        {
                            ProcessNugetPackage(log, targetDir, manualDir, processedPacks, unprocessedDependencies, platform, pack);
                        }
                    }
                }
            }
        }

        public static string ProcessVersion(string name, string version)
        {
            version = version.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "");

            if (version.Contains(','))
            {
                var vers = version.Split(',');
                version = null;
                foreach (string ver in vers)
                {
                    string nuspec = Path.Combine(Path.Combine(_nugetDir, name, ver), String.Format("{0}.nuspec", name));
                    if (File.Exists(nuspec))
                    {
                        version = ver;
                        break;
                    }
                }
            }

            if (version != null)
            {
                string nuspec = Path.Combine(Path.Combine(_nugetDir, name, version), String.Format("{0}.nuspec", name));
                if (!File.Exists(nuspec))
                {
                    version = null;
                }
            }

            if (version == null)
            {
                string dir = Path.Combine(_nugetDir, name);
                if (Directory.Exists(dir))
                {
                    version = Directory.GetDirectories(dir)
                                .Select(s => Path.GetFileName(s))
                                .OrderByDescending(s => s)
                                .FirstOrDefault();
                }
            }

            return version;
        }

        public static void Delete(string dir)
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                try
                {
                    Thread.Sleep(2000);
                    Directory.Delete(dir, true);
                }
                catch
                {
                }
            }
        }
    }
}