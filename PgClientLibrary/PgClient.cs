using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PgClientLibrary {
	public class PgClient {
		private static readonly PgClient _instance = new PgClient();
		public static PgClient Instance { get { return _instance; } }

		public Exception StartupException { get; private set; }

		private readonly AnonymousPipeClientStream _pipeClient;
		private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
		private readonly TimeSpan _cancellationTime = TimeSpan.FromMilliseconds(10);

		// private static Process _process = Process.GetCurrentProcess();

		static PgClient() { }

		private PgClient() {
			try {
				var pipeHandle = Environment.GetEnvironmentVariable(PgClientLib.EnvironmentKeyName);
				if (pipeHandle == null) {
					throw new Exception($"Environment variable {PgClientLib.EnvironmentKeyName} containing pipe handle not set");
				}

				_pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, pipeHandle);
				StartupException = null;
			} catch (Exception e) {
				StartupException = e;
			}
		}

		public async Task KeepAlive(Action<string> log) {
			if (StartupException != null) {
				return;
			}
			try {
				using (var cts = new CancellationTokenSource(_cancellationTime)) {
					try {
						await WriteDateTime(DateTime.Now, cts.Token).ConfigureAwait(false);
					} catch (OperationCanceledException) { }
				}
			} catch (Exception e) {
				try {
					log($"PgClient: {e.Message}");
				} catch { }
			}
		}

		private async Task WriteDateTime(DateTime dt, CancellationToken token) {
			var dtLong = dt.ToBinary();
			var bArray = BitConverter.GetBytes(dtLong);

			// await Task.Delay(30, token);
			await _semaphoreSlim.WaitAsync(token).ConfigureAwait(false);
			try {
				// Console.WriteLine($"PgClient [{_process.Id}]: Sent");
				await _pipeClient.WriteAsync(bArray, 0, bArray.Length, token).ConfigureAwait(false);
			} finally {
				_semaphoreSlim.Release();
			}
		}
	}
}
