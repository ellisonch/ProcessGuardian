using PgClientLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Specialized;

namespace ProcessGuardian {
	public class Monitor {
		private readonly string _path;
		private readonly string _executableName;
		private readonly string _extension;

		private readonly string _fullFilename;
		private readonly string _pathAndFilename;
		private readonly TimeSpan _pollResolution = TimeSpan.FromSeconds(1);

		private readonly TimeSpan _timeBeforeRestart;
		private readonly TimeSpan _waitTimeOnKill;
		private readonly TimeSpan _maxRestartDelay;
		private readonly TimeSpan _minRestartDelay = TimeSpan.FromSeconds(5);

		private TimeSpan _currentRestartDelay;
		private DateTime _lastSeen = DateTime.MinValue;

		public Monitor() {
			var settings = (NameValueCollection)ConfigurationManager.GetSection("guardianSettings");
			_path = settings["path"];
			_executableName = settings["executable"];
			_extension = settings["extension"];

			_timeBeforeRestart = TimeSpan.Parse(settings["maxTimeWithoutRefresh"]);
			_waitTimeOnKill = TimeSpan.Parse(settings["timeToShutdownProcess"]);
			_maxRestartDelay = TimeSpan.Parse(settings["timeBetweenRestart"]);

			_fullFilename = _executableName + _extension;
			if (Path.GetFileName(_fullFilename) != _fullFilename) {
				throw new Exception($"{_fullFilename} is invalid!");
			}
			_pathAndFilename = Path.Combine(_path, _fullFilename);


			Log($"Executing {_pathAndFilename} with settings {_timeBeforeRestart}, {_waitTimeOnKill}, and {_maxRestartDelay}");
		}
		
		internal async Task Start() {
			Log("Monitor starting up...");
			var id = 0;
			while (true) {
				_currentRestartDelay = _maxRestartDelay;

				id++;
				try {
					await RunOnce(id).ConfigureAwait(false);
				} catch (Exception e) {
					Log($"{id}: {e}");
				}
				Log($"{id}: RunOnce() terminated, waiting {_currentRestartDelay}...");
				await Task.Delay(_currentRestartDelay).ConfigureAwait(false);
				Log($"{id}: Starting up again...");
			}
		}
		internal async Task RunOnce(int id) {
			KillProcess();

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
					// Log($"{id}: Refreshed fast enough ({latency.TotalMilliseconds}ms)");
				}

				var sw = Stopwatch.StartNew();
				var taskResult = await TimeoutAfter(readTask, _pollResolution);
				if (taskResult.HasValue) {
					// if the task completed, handle the result and create a new task
					var now = taskResult.Value;
					_lastSeen = now;
					readTask = ReadDateTime(pipeServer);

					Log($"{id}: Read dt of {now}; restart delay is {_currentRestartDelay}");
				}

				var newCurrent = _currentRestartDelay.Subtract(new TimeSpan(sw.ElapsedTicks / 4));
				if (newCurrent >= _minRestartDelay) {
					_currentRestartDelay = newCurrent;
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
					} else if (task.Status == TaskStatus.Faulted) {
						throw task.Exception;
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
			var numbytes = await pipeServer.ReadAsync(buffer, 0, sizeof(long)).ConfigureAwait(false); // readasync ignores tokens :(
			if (numbytes < sizeof(long)) {
				throw new Exception("less than sizeof(long) bytes read");
			}
			var nowLong = BitConverter.ToInt64(buffer, 0);
			var now = DateTime.FromBinary(nowLong);
			return now;
		}

		private void KillProcess() {
			Process[] processes = Process.GetProcessesByName(_executableName);
			foreach (var proc in processes) {
				Log($"Killing {proc}...");
				proc.Kill();
				if (!proc.WaitForExit((int)_waitTimeOnKill.TotalMilliseconds)) {
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
