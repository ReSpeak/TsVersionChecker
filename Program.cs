using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VersionChecker
{
	public static class Program
	{
		public static readonly byte[] Ts3VerionSignPublicKey = Convert.FromBase64String("UrN1jX0dBE1vulTNLCoYwrVpfITyo+NBuq/twbf9hLw=");

		public static int Main(string[] args)
		{
			var dataNew = File.ReadAllText("Versions.csv");
			var duplicatesNew = new Dictionary<string, bool>();

			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = "git",
				UseShellExecute = false,
				Arguments = "show origin/master:Versions.csv",
				RedirectStandardOutput = true,
			});

			var dataOld = proc.StandardOutput.ReadToEnd();
			var duplicatesOld = new Dictionary<string, bool>();

			bool okNew = CheckFile(dataNew, duplicatesNew);
			CheckFile(dataOld, duplicatesOld);

			foreach (var oldEntry in duplicatesOld)
			{
				if (!oldEntry.Value)
					continue;

				if(!duplicatesNew.ContainsKey(oldEntry.Key))
				{
					Console.WriteLine("You new file is missing a version which was previously included: {0}", oldEntry.Key);
					okNew = false;
				}
			}

			return okNew ? 0 : 1;
		}

		// Hacky csv parser, but should be enough for this file.
		private static bool CheckFile(string data, Dictionary<string, bool> duplicates)
		{
			if (data.Contains('\r'))
			{
				Console.WriteLine("File is not using consistent \\n line endigns.");
				return false;
			}

			var lines = data.Split('\n');
			if (string.IsNullOrEmpty(lines.Last()))
			{
				lines = lines.Take(lines.Length - 1).ToArray();
			}

			var header = lines[0].Split(',');
			int ichan = Array.IndexOf(header, "channel");
			int iname = Array.IndexOf(header, "version");
			int iplat = Array.IndexOf(header, "platform");
			int ihash = Array.IndexOf(header, "hash");

			bool allOk = true;
			foreach (var line in lines.Skip(1))
			{
				try
				{
					var split = line.Split(',');

					string chan = ichan != -1 ? split[ichan] : null;
					string name = split[iname];
					string platform = split[iplat];
					string hash = split[ihash];

					string unique = platform + " " + name;

					if (duplicates.ContainsKey(unique))
					{
						Console.WriteLine("Duplicate: {0}", line);
						allOk = false;
						continue;
					}

					var ok = CheckLine(line, name, platform, hash, chan);

					if (ok)
						duplicates.Add(unique, ok);

					allOk &= ok;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Invalid line ({0}): {1}", ex.Message, line);
					allOk = false;
				}
			}

			return allOk;
		}

		private static bool CheckLine(string line, string name, string platform, string sign, string channel)
		{
			if (channel != "Alpha" && channel != "Beta" && channel != "Stable" && !string.IsNullOrEmpty(channel))
			{
				Console.WriteLine("Unrecognized channel: {0}", line);
				return false;
			}

			var ver = Encoding.ASCII.GetBytes(platform + name);

			if (!Chaos.NaCl.Ed25519.Verify(Convert.FromBase64String(sign), ver, Ts3VerionSignPublicKey))
			{
				Console.WriteLine("Sign invalid: {0}", line);
				return false;
			}

			return true;
		}
	}
}
