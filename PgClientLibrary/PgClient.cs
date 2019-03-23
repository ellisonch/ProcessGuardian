using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PgClientLibrary {
	public class PgClient {
		private readonly AnonymousPipeClientStream _pipeClient;
		//private readonly StreamWriter _sw;

		public PgClient() {
			var pipeHandle = Environment.GetEnvironmentVariable(PgClientLib.EnvironmentKeyName);
			if (pipeHandle == null) {
				throw new Exception("Environment variable containing pipe handle not set");
			}

			// using (PipeStream pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, pipe)) {
			// }
			_pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, pipeHandle);
			//_sw = new StreamWriter(_pipeClient);
		}

		public Task KeepAlive() {
			var now = DateTime.Now.ToBinary();
			var bArray = BitConverter.GetBytes(now);
			return _pipeClient.WriteAsync(bArray, 0, bArray.Length);
		}
	}
}
