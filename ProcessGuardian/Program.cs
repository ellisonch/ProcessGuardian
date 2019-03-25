using System;
using System.Threading.Tasks;

namespace ProcessGuardian {
	class Program {
		static async Task Main(string[] args) {
			bool ok;
			string splat = "dev";
			using (var m = new System.Threading.Mutex(true, "Global\\ProcessGuardian" + splat, out ok)) {
				if (!ok) {
					Console.WriteLine("Another instance is already running.");
					Environment.Exit(1);
				}
				Monitor mon = new Monitor();
				await mon.Start();
			}			
		}
	}
}



