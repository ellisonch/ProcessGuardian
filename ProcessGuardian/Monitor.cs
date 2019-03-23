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
		private string _path = @"../../../PgClientExample/bin/Debug/PgClientExample.exe";

		public async Task Watch() {
			//var process = GetProcess(_path);
			// process.WaitForExit();// Waits here for the process to exit.
			await this.Server();
		}

		private async Task Server() {
			var process = GetProcess(_path);

			using (AnonymousPipeServerStream pipeServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable)) {
				Console.WriteLine("[SERVER] Current TransmissionMode: {0}.", pipeServer.TransmissionMode);

				// Pass the client process a handle to the server.
				process.StartInfo.Environment.Add(PgClientLib.EnvironmentKeyName, pipeServer.GetClientHandleAsString());
				process.StartInfo.UseShellExecute = false;
				process.Start();

				pipeServer.DisposeLocalCopyOfClientHandle();

				try {
					while (true) {
						var buffer = new byte[sizeof(long)];
						await pipeServer.ReadAsync(buffer, 0, sizeof(long));
						var nowLong = BitConverter.ToInt64(buffer, 0);
						var now = DateTime.FromBinary(nowLong);
						Console.WriteLine($"Read dt of {now}");
					}
					// Read user input and send that to the client process.
					//using (StreamReader sw = new StreamReader(pipeServer)) {
						// var msg = await sw.ReadLineAsync();
						// Console.WriteLine($"Read {msg}");
						//sw.AutoFlush = true;
						// Send a 'sync message' and wait for client to receive it.
						//sw.WriteLine("SYNC");
						//pipeServer.WaitForPipeDrain();
						// Send the console input to the client process.
						//Console.Write("[SERVER] Enter text: ");
						//sw.WriteLine(Console.ReadLine());
					//}
				}
				// Catch the IOException that is raised if the pipe is broken or disconnected.
				catch (IOException e) {
					Console.WriteLine("[SERVER] Error: {0}", e.Message);
				}
			}

			process.WaitForExit();
			process.Close();
			Console.WriteLine("[SERVER] Client quit. Server terminating.");
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
