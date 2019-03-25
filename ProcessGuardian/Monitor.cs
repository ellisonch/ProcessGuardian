using PgClientLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProcessGuardian {
	public class Monitor {
		// private string _path = @"D:\paladins\DiscordBot\Application Files\DiscordBot_1_4_4_0[24]\DiscordBot.exe";
		private readonly string _path = @"../../../PgClientExample/bin/Debug/";
		private readonly string _executableName = @"PgClientExample";
		private readonly string _extension = ".exe";

		private readonly string _fullFilename;
		private readonly string _pathAndFilename;
		private readonly TimeSpan _timeBeforeRestart = TimeSpan.FromSeconds(5);
		private readonly TimeSpan _refreshTime = TimeSpan.FromSeconds(1);
		private readonly TimeSpan _killDelay = TimeSpan.FromSeconds(5);
		private readonly TimeSpan _restartDelay = TimeSpan.FromSeconds(5);

		private DateTime _lastSeen = DateTime.MinValue;

		public Monitor() {
			_fullFilename = _executableName + _extension;
			if (Path.GetFileName(_fullFilename) != _fullFilename) {
				throw new Exception($"{_fullFilename} is invalid!");
			}
			_pathAndFilename = Path.Combine(_path, _fullFilename);
		}

		internal async Task Start() {
			Log("Monitor starting up...");
			var id = 0;
			while (true) {
				id++;
				await RunOnce(id).ConfigureAwait(false);
				Log($"{id}: RunOnce() terminated, waiting {_restartDelay}...");
				await Task.Delay(_restartDelay).ConfigureAwait(false);
				Log($"{id}: Starting up again...");
			}
		}
		internal async Task RunOnce(int id) {
			//GC.Collect();
			//GC.WaitForPendingFinalizers();
			//GC.Collect();
			KillProcess();

			// var process = GetProcess(_pathAndFilename);
			using (var process = GetProcess(_pathAndFilename))
			using (var pipeServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable)) {
				process.StartInfo.Environment.Add(PgClientLib.EnvironmentKeyName, pipeServer.GetClientHandleAsString());

				process.Start();
				// The DisposeLocalCopyOfClientHandle method should be called after the client handle has been passed to the client. If this method is not called, the AnonymousPipeServerStream object will not receive notice when the client disposes of its PipeStream object.
				pipeServer.DisposeLocalCopyOfClientHandle();

				await InnerLoop(pipeServer, id).ConfigureAwait(false);
				try {
					if (!process.HasExited) {
						process.Kill();
					}
				} catch { }
			}
		}

		private async Task InnerLoop(AnonymousPipeServerStream pipeServer, int id) {
			_lastSeen = DateTime.Now;
			var readTask = ReadDateTime(pipeServer);

			while (true) {
				var latency = DateTime.Now - _lastSeen;
				if (latency > _timeBeforeRestart) {
					Log($"{id}: Need to restart, not seen fast enough ({latency.TotalMilliseconds}ms)");
					return;
				} else {
					Log($"{id}: Refreshed fast enough ({latency.TotalMilliseconds}ms)");
				}

				var taskResult = await TimeoutAfter(readTask, _refreshTime);
				if (taskResult.HasValue) {
					// if the task completed, handle the result and create a new task
					var now = taskResult.Value;
					_lastSeen = now;
					readTask = ReadDateTime(pipeServer);
					Log($"{id}: Read dt of {now}");
				}
			}
		}

		private void Log(string v) {
			Console.WriteLine(v);
		}

		// from https://stackoverflow.com/a/22078975/2877032
		private static async Task<TResult?> TimeoutAfter<TResult>(Task<TResult> task, TimeSpan timeout) where TResult : struct {
			using (var timeoutCancellationTokenSource = new CancellationTokenSource()) {
				var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
				if (completedTask == task) {
					timeoutCancellationTokenSource.Cancel();
					if (task.Status == TaskStatus.RanToCompletion) {
						return task.Result;
					}
					return null;
					// return await task;  // Very important in order to propagate exceptions
					// return true;
				} else {
					return null;
					// throw new TimeoutException("The operation has timed out.");
				}
			}
		}

		private static async Task<DateTime> ReadDateTime(AnonymousPipeServerStream pipeServer) {
			var buffer = new byte[sizeof(long)];
			await pipeServer.ReadAsync(buffer, 0, sizeof(long)).ConfigureAwait(false); // readasync ignores tokens :(
			var nowLong = BitConverter.ToInt64(buffer, 0);
			var now = DateTime.FromBinary(nowLong);
			return now;
		}

		private void KillProcess() {
			Process[] processes = Process.GetProcessesByName(_executableName);
			foreach (var proc in processes) {
				Log($"Killing {proc}...");
				proc.Kill();
				if (!proc.WaitForExit((int)_killDelay.TotalMilliseconds)) {
					throw new Exception("Couldn't kill process");
				}
			}
			processes = Process.GetProcessesByName(_executableName);
			if (processes.Length > 0) {
				throw new Exception("Couldn't kill process");
			}
		}

		private static Process GetProcess(string path) {
			Process process = new Process();
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = false;
			process.StartInfo.FileName = path;
			process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
			return process;
		}
	}
}
