using System;
using System.Collections.Generic;
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

		private readonly AnonymousPipeClientStream _pipeClient;
		private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
		private static TimeSpan _cancellationTime = TimeSpan.FromMilliseconds(10);

		static PgClient() { }

		private PgClient() {
			var pipeHandle = Environment.GetEnvironmentVariable(PgClientLib.EnvironmentKeyName);
			if (pipeHandle == null) {
				throw new Exception($"Environment variable {PgClientLib.EnvironmentKeyName} containing pipe handle not set");
			}

			_pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, pipeHandle);
		}

		public async Task KeepAlive() {
			using (var cts = new CancellationTokenSource(_cancellationTime)) {
				try {
					await WriteDateTime(DateTime.Now, cts.Token).ConfigureAwait(false);
				} catch (OperationCanceledException) { }
			}
		}

		private async Task WriteDateTime(DateTime dt, CancellationToken token) {
			var dtLong = dt.ToBinary();
			var bArray = BitConverter.GetBytes(dtLong);

			// await Task.Delay(30, token);
			await _semaphoreSlim.WaitAsync(token).ConfigureAwait(false);
			try {
				await _pipeClient.WriteAsync(bArray, 0, bArray.Length, token).ConfigureAwait(false);
			} finally {
				_semaphoreSlim.Release();
			}
		}
	}
}
