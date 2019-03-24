using PgClientLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessGuardian {
	public class Monitor {
		// private string _path = @"D:\paladins\DiscordBot\Application Files\DiscordBot_1_4_4_0[24]\DiscordBot.exe";
		private readonly string _path = @"../../../PgClientExample/bin/Debug/";
		private readonly string _executableName = @"PgClientExample.exe";
		private readonly string _pathAndFilename;

		public Monitor() {
			if (Path.GetFileName(_executableName) != _executableName) {
				throw new Exception($"{_executableName} is invalid!");
			}
			_pathAndFilename = Path.Combine(_path, _executableName);
		}

		internal async Task Start() {
			Console.WriteLine("Monitor starting up...");
			while (true) {
				await RunOnce().ConfigureAwait(false);
				Console.WriteLine("RunOnce() terminated... starting up again...");
			}
		}
		internal async Task RunOnce() {
			KillProcess();

			// var process = GetProcess(_pathAndFilename);
			using (var process = GetProcess(_pathAndFilename))
			using (var pipeServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable)) {
				process.StartInfo.Environment.Add(PgClientLib.EnvironmentKeyName, pipeServer.GetClientHandleAsString());
				process.StartInfo.UseShellExecute = false;

				process.Start();
				// The DisposeLocalCopyOfClientHandle method should be called after the client handle has been passed to the client. If this method is not called, the AnonymousPipeServerStream object will not receive notice when the client disposes of its PipeStream object.
				pipeServer.DisposeLocalCopyOfClientHandle();

				await InnerLoop(pipeServer).ConfigureAwait(false);
			}
		}

		private static async Task InnerLoop(AnonymousPipeServerStream pipeServer) {
			while (true) {
				var now = await ReadDateTime(pipeServer).ConfigureAwait(false);
				Console.WriteLine($"Read dt of {now}");
			}
		}

		private static async Task<DateTime> ReadDateTime(AnonymousPipeServerStream pipeServer) {
			var buffer = new byte[sizeof(long)];
			await pipeServer.ReadAsync(buffer, 0, sizeof(long)).ConfigureAwait(false);
			var nowLong = BitConverter.ToInt64(buffer, 0);
			var now = DateTime.FromBinary(nowLong);
			return now;
		}

		private void KillProcess() {
			Process[] processes = Process.GetProcessesByName(_executableName);
			foreach (var proc in processes) {
				Console.WriteLine($"Killing {proc}...");
				proc.Kill();
			}
			processes = Process.GetProcessesByName(_executableName);
			if (processes.Length > 0) {
				throw new Exception("Couldn't kill process");
			}
		}

		private static Process GetProcess(string path) {
			Process process = new Process();
			// Configure the process using the StartInfo properties.
			process.StartInfo.FileName = path;
			// process.StartInfo.Arguments = "-n";
			process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
			return process;
		}
	}
}
