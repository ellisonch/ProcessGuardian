using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PgClientExample {
	class Program {
		private static Random _r = new Random();
		static void Main(string[] args) {
			DoThing().Wait();
		}

		private static async Task DoThing() {
			var pg = PgClientLibrary.PgClient.Instance;

			while (true) {
				//Console.Write("Running KeepAlive...");
				await pg.KeepAlive(v => Console.WriteLine(v));
				//Console.Write("done.\n");

				//Console.Write("Sleeping for 1s...");
				await Task.Delay(_r.Next(7000));
				//Console.Write("done.\n");
			}
		}
	}
}
