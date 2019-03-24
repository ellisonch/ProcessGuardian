﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PgClientExample {
	class Program {
		static void Main(string[] args) {
			DoThing().Wait();
		}

		private static async Task DoThing() {
			var pg = PgClientLibrary.PgClient.Instance;

			while (true) {
				Console.Write("Running KeepAlive...");
				await pg.KeepAlive();
				Console.Write("done.\n");

				Console.Write("Sleeping for 1s...");
				Thread.Sleep(1000);
				Console.Write("done.\n");
			}
		}
	}
}
