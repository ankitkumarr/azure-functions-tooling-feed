﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace GenerateToolingFeed
{
    class Program
    {
        private const string _feedUrl = "https://raw.githubusercontent.com/Azure/azure-functions-tooling-feed/master/cli-feed-v3.json";

        static void Main(string[] args)
        {
            string cliVersion = GetCliVersion(args[0]);
            Console.WriteLine($"Preparing CLI feed for version:{cliVersion}");

            string coreToolsArtifactsDirectory = args[0];

            var currentFeed = Helper.HttpClient.GetStringAsync(_feedUrl).Result;
            var currentFeedJson = JObject.Parse(currentFeed);
            var targetFeedJson = JObject.Parse(currentFeed);

            string releaseVersion = Helper.GetReleaseVersionFromTag(currentFeedJson, tag: "v2");

            var releaseJson = currentFeedJson["releases"][releaseVersion];
            var feedEntry = releaseJson.ToObject<FeedEntry>();

            var feedReleaseVersion = Helper.GetReleaseVersion(currentFeedJson["releases"]);

            // Update the standalone entries
            var updatedCliEntry = UpdateStandaloneCli(feedEntry, coreToolsArtifactsDirectory, cliVersion);

            var cliEtnry = new CliEntry { OS = "windows", Architecture = "x86" };
            releaseJson["cli"] = Helper.GetDownloadLink(cliEtnry, cliVersion, isMinified: true);
            releaseJson["sha2"] = Helper.GetShaFileContent(cliEtnry, cliVersion, coreToolsArtifactsDirectory, isMinified: true);
            releaseJson["standaloneCli"] = JArray.FromObject(updatedCliEntry);
            releaseJson["itemTemplates"] = Helper.GetTemplateUrl("Microsoft.Azure.WebJobs.ItemTemplates");
            releaseJson["projectTemplates"] = Helper.GetTemplateUrl("Microsoft.Azure.WebJobs.ProjectTemplates");

            var targetFeedReleases = targetFeedJson["releases"];
            ((JObject)targetFeedReleases).Add(feedReleaseVersion, releaseJson);
            targetFeedJson["tags"]["v2-prerelease"]["release"] = feedReleaseVersion;

            string path = Path.Combine(coreToolsArtifactsDirectory, "cli-feed-v3.json");
            string feedString = JsonConvert.SerializeObject(targetFeedJson, Formatting.Indented);

            Console.WriteLine("Writing File\n" + feedString);
            File.WriteAllText(path, feedString);
        }

        private static string GetCliVersion(string path)
        {
            string cliVersion = string.Empty;
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".zip"))
                {
                    var zip = ZipFile.OpenRead(file);
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name == "func.dll")
                        {
                            ZipFileExtensions.ExtractToFile(entry, "func.dll", true);
                            cliVersion = FileVersionInfo.GetVersionInfo(".\\func.dll").FileVersion;
                            break;
                        }
                    }
                }
            }
            return cliVersion;
        }

        private static CliEntry[] UpdateStandaloneCli(FeedEntry feedEntry, string coreToolsArtifactsDirectory, string cliVersion)
        {
            foreach (var cliEntry in feedEntry.standaloneCli)
            {
                // Updating sha2
                cliEntry.sha2 = Helper.GetShaFileContent(cliEntry, cliVersion, coreToolsArtifactsDirectory);
                cliEntry.downloadLink = Helper.GetDownloadLink(cliEntry, cliVersion);
            }
            return feedEntry.standaloneCli;
        }
    }
}